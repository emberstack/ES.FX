# Normalizes the committed Zendesk OpenAPI snapshots into generator-ready specs.
#
# Zendesk's published specs contain constructs that break Kiota (discriminator-less polymorphism)
# or that diverge from live-verified API behavior (Help Center requires the `.json` path suffix).
# Every transformation below is a *recorded patch*: it asserts the expected drift-sensitive content
# is still present and FAILS LOUDLY when Zendesk changes the schema, forcing a human review of the
# patch instead of silently producing a wrong client.
#
# Output: *.normalized.yaml next to the snapshots (git-ignored; regenerate at will).
# Usage:  ./normalize.ps1 [-Refresh]   # -Refresh re-downloads the snapshots from developer.zendesk.com

[CmdletBinding()]
param(
    # Re-download the spec snapshots from developer.zendesk.com before normalizing.
    [switch]$Refresh
)

$ErrorActionPreference = 'Stop'

$specs = @(
    [pscustomobject]@{
        Name     = 'support'
        Url      = 'https://developer.zendesk.com/zendesk/oas.yaml'
        Snapshot = Join-Path $PSScriptRoot 'support-oas.yaml'
    }
    [pscustomobject]@{
        Name     = 'helpcenter'
        Url      = 'https://developer.zendesk.com/help_center/oas.yaml'
        Snapshot = Join-Path $PSScriptRoot 'helpcenter-oas.yaml'
    }
)

if ($Refresh) {
    foreach ($spec in $specs) {
        Write-Host "Downloading $($spec.Url) ..."
        Invoke-WebRequest -Uri $spec.Url -OutFile $spec.Snapshot
    }
}

# Replaces the component schema named $SchemaName (4-space indent under components/schemas) with
# $Replacement. Asserts the current block still contains every $MustContain needle so that spec
# drift surfaces as a hard error instead of a silently mis-applied patch.
function Replace-SchemaBlock {
    param(
        [string[]]$Lines,
        [string]$SchemaName,
        [string[]]$MustContain,
        [string[]]$Replacement
    )

    $start = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -ceq "    ${SchemaName}:") {
            if ($start -ge 0) { throw "Multiple definitions of schema '$SchemaName' found; patch is ambiguous." }
            $start = $i
        }
    }
    if ($start -lt 0) { throw "Schema '$SchemaName' not found - the spec has drifted; review this patch." }

    $end = $Lines.Count
    for ($i = $start + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^    \S') { $end = $i; break }
    }

    $block = $Lines[$start..($end - 1)] -join "`n"
    foreach ($needle in $MustContain) {
        if (-not $block.Contains($needle)) {
            throw "Schema '$SchemaName' no longer contains '$needle' - the spec has drifted; review this patch."
        }
    }

    $result = [System.Collections.Generic.List[string]]::new()
    if ($start -gt 0) { $result.AddRange([string[]]$Lines[0..($start - 1)]) }
    $result.AddRange([string[]]$Replacement)
    if ($end -lt $Lines.Count) { $result.AddRange([string[]]$Lines[$end..($Lines.Count - 1)]) }
    return $result.ToArray()
}

# ---------------------------------------------------------------------------------------------
# Support API
# ---------------------------------------------------------------------------------------------
$support = $specs | Where-Object Name -eq 'support'
$lines = Get-Content $support.Snapshot

# P1: UserObject is `anyOf [UserForAdmin, UserForEndUser]` with no discriminator - fatal for Kiota
# ("The type does not contain any information ... Reference Id: UserObject"). UserForAdmin is the
# superset view (admin responses include every end-user field), so collapse to it.
$lines = Replace-SchemaBlock -Lines $lines -SchemaName 'UserObject' `
    -MustContain @('anyOf', 'UserForAdmin', 'UserForEndUser') `
    -Replacement @(
        '    UserObject:',
        '      allOf:',
        "        - `$ref: '#/components/schemas/UserForAdmin'",
        '      additionalProperties: true'
    )

# P2: TicketsUpdateRequest is a discriminator-less oneOf of {ticket: ...} (single update) and
# {tickets: [...]} (batch update). The variants have disjoint property names, so a merged object
# carrying both optional properties is wire-identical for any correctly formed request. The variant
# payload shapes are hoisted into NAMED schemas (TicketUpdateInput / TicketBatchUpdateInput) so Kiota
# generates well-named model classes instead of anonymous nested types.
$lines = Replace-SchemaBlock -Lines $lines -SchemaName 'TicketsUpdateRequest' `
    -MustContain @('oneOf', 'ticket:', 'tickets:', 'additional_tags', 'remove_tags') `
    -Replacement @(
        '    TicketUpdateInput:',
        '      allOf:',
        "        - `$ref: '#/components/schemas/TicketObject'",
        '        - type: object',
        '          properties:',
        '            additional_tags:',
        '              type: array',
        '              description: Tags to add to existing tags without overwriting',
        '              items:',
        '                type: string',
        '            remove_tags:',
        '              type: array',
        '              description: Tags to remove from the ticket',
        '              items:',
        '                type: string',
        '    TicketBatchUpdateInput:',
        '      allOf:',
        "        - `$ref: '#/components/schemas/TicketObject'",
        '        - type: object',
        '          properties:',
        '            additional_tags:',
        '              type: array',
        '              description: Tags to add to existing tags without overwriting',
        '              items:',
        '                type: string',
        '            id:',
        '              type: integer',
        '              format: int64',
        '              description: The ID of the ticket to update',
        '            remove_tags:',
        '              type: array',
        '              description: Tags to remove from the ticket',
        '              items:',
        '                type: string',
        '    TicketsUpdateRequest:',
        '      type: object',
        '      properties:',
        '        ticket:',
        "          `$ref: '#/components/schemas/TicketUpdateInput'",
        '        tickets:',
        '          type: array',
        '          items:',
        "            `$ref: '#/components/schemas/TicketBatchUpdateInput'"
    )

# P3: UserInput is `anyOf [UserCreateInput, UserMergeInput]` with no discriminator. The two variants
# describe the same user fields with consistent types (merge adds the id/email/external_id
# identification properties), so an allOf merge yields the correct union input model.
$lines = Replace-SchemaBlock -Lines $lines -SchemaName 'UserInput' `
    -MustContain @('anyOf', 'UserCreateInput', 'UserMergeInput') `
    -Replacement @(
        '    UserInput:',
        '      allOf:',
        "        - `$ref: '#/components/schemas/UserCreateInput'",
        "        - `$ref: '#/components/schemas/UserMergeInput'",
        '      additionalProperties: true'
    )

# P4: JobStatusResultObject is a discriminator-less oneOf of CreateResourceResult /
# UpdateResourceResult / FailedResult. Kiota cannot pick a variant when deserializing, so flatten to
# the union of the three (property names/types are consistent across variants; nothing invented).
$lines = Replace-SchemaBlock -Lines $lines -SchemaName 'JobStatusResultObject' `
    -MustContain @('oneOf', 'CreateResourceResult', 'UpdateResourceResult', 'FailedResult') `
    -Replacement @(
        '    JobStatusResultObject:',
        '      type: object',
        '      properties:',
        '        action:',
        '          type: string',
        '          description: The action the job attempted (e.g. "update")',
        '        details:',
        '          type: string',
        '          description: The details of the error, if the action failed',
        '        error:',
        '          type: string',
        '          description: The error message, if the action failed',
        '        id:',
        '          type: integer',
        '          format: int64',
        '          description: The id of the resource the job acted on',
        '        index:',
        '          type: integer',
        '          description: The index number of the result',
        '        status:',
        '          type: string',
        '          description: The status of the action (e.g. "Updated")',
        '        success:',
        '          type: boolean',
        '          description: Whether the action was successful or not',
        '      additionalProperties: true'
    )

$supportOut = Join-Path $PSScriptRoot 'support-oas.normalized.yaml'
Set-Content -Path $supportOut -Value $lines
Write-Host "support: 4 schema patches applied -> $supportOut"

# ---------------------------------------------------------------------------------------------
# Help Center API
# ---------------------------------------------------------------------------------------------
$helpcenter = $specs | Where-Object Name -eq 'helpcenter'
$lines = Get-Content $helpcenter.Snapshot

# P5: Help Center requires the `.json` path suffix - live-verified: extension-less paths returned
# HTTP 415 even with JSON headers. The published spec omits the suffix, so append it to every path
# key (the `.json` form is the one the retired hand-written client used successfully in production).
$pathCount = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^(  /[^\s:]+):\s*$') {
        $path = $Matches[1]
        if ($path.EndsWith('.json')) { throw "Path '$path' already ends in .json - the spec has drifted; review this patch." }
        $lines[$i] = "${path}.json:"
        $pathCount++
    }
}
if ($pathCount -eq 0) { throw 'No Help Center paths found - the spec has drifted; review this patch.' }

$helpcenterOut = Join-Path $PSScriptRoot 'helpcenter-oas.normalized.yaml'
Set-Content -Path $helpcenterOut -Value $lines
Write-Host "helpcenter: .json suffix appended to $pathCount paths -> $helpcenterOut"

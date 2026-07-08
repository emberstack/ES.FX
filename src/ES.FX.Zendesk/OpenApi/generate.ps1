# Regenerates the Kiota clients in ../Generated from the committed spec snapshots in this folder.
#
# The generated output is git-ignored and produced at build time: the ES.FX.Zendesk project invokes this
# script from an incremental MSBuild target (see README.md), and it can also be run by hand.
# Usage: ./generate.ps1 [-Refresh]   # -Refresh re-downloads the spec snapshots first

[CmdletBinding()]
param(
    # Re-download the spec snapshots from developer.zendesk.com before normalizing/generating.
    [switch]$Refresh
)

$ErrorActionPreference = 'Stop'

# Kiota 1.34.0 generation is not fully deterministic; forcing single-threaded generation eliminates the
# structural variance (occasional dropped types) observed under the default parallelism, which is the
# variant most likely to fail compilation. See README.md.
$env:KIOTA_GENERATION_MAXDEGREEOFPARALLELISM = '1'

# This script lives in src/ES.FX.Zendesk/OpenApi. Resolve the project directory (for the output) and the
# repo root (where the .config/dotnet-tools.json tool manifest lives).
$projectDirectory = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path (Split-Path $projectDirectory -Parent) -Parent

& (Join-Path $PSScriptRoot 'normalize.ps1') -Refresh:$Refresh

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    # --log-level Error: the spec's OpenAPI warnings (missing discriminators, unsupported formats) are
    # known and handled by normalize.ps1; silencing them keeps the build-time log clean.
    dotnet kiota generate --language CSharp `
        --openapi (Join-Path $PSScriptRoot 'support-oas.normalized.yaml') `
        --class-name ZendeskSupportApiClient `
        --namespace-name ES.FX.Zendesk.Support `
        --output (Join-Path $projectDirectory 'Generated/Support') `
        --exclude-backward-compatible `
        --clean-output `
        --log-level Error
    if ($LASTEXITCODE -ne 0) { throw 'Kiota generation failed for the Support client.' }

    dotnet kiota generate --language CSharp `
        --openapi (Join-Path $PSScriptRoot 'helpcenter-oas.normalized.yaml') `
        --class-name ZendeskHelpCenterApiClient `
        --namespace-name ES.FX.Zendesk.HelpCenter `
        --output (Join-Path $projectDirectory 'Generated/HelpCenter') `
        --exclude-backward-compatible `
        --clean-output `
        --log-level Error
    if ($LASTEXITCODE -ne 0) { throw 'Kiota generation failed for the Help Center client.' }
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------------------------
# Post-generation repair: force every discriminator factory to match its own generic type argument.
#
# Kiota 1.34.0 non-deterministically emits the *base* type's CreateFromDiscriminatorValue for a
# property/collection whose declared element type is a derived (allOf) model - e.g.
#   GetObjectValue<CustomObjectField>(CustomFieldObject.CreateFromDiscriminatorValue)
# which fails to compile with CS0407 ("wrong return type"). Whether a given type lands on the broken
# variant shifts with generation order (parallelism, and Windows vs. the Linux container build - see
# the KIOTA_GENERATION_MAXDEGREEOFPARALLELISM note above), so it is not reliably reproducible.
#
# Correct Kiota output ALWAYS calls the factory of the generic argument itself - genuine polymorphism
# uses the base type for BOTH the generic argument and the factory - so there is no valid case where
# the two differ. Forcing factory := generic-argument therefore repairs the defective variant and is
# a no-op on correct output, making generation deterministic. See README.md.
$factoryPattern = '(?<call>GetObjectValue|GetCollectionOfObjectValues)<(?<t>[^>]+)>\(\s*[^()]+?\.CreateFromDiscriminatorValue\s*\)'
$factoryReplacement = '${call}<${t}>(${t}.CreateFromDiscriminatorValue)'
$repairedFiles = 0
Get-ChildItem -Path (Join-Path $projectDirectory 'Generated') -Recurse -Filter *.cs | ForEach-Object {
    $original = Get-Content -Raw -LiteralPath $_.FullName
    $repaired = [regex]::Replace($original, $factoryPattern, $factoryReplacement)
    if ($repaired -ne $original) {
        Set-Content -LiteralPath $_.FullName -Value $repaired -NoNewline
        $repairedFiles++
    }
}
Write-Host "factory repair: matched CreateFromDiscriminatorValue to generic type in $repairedFiles file(s)"

Write-Host 'Generation complete. Review the diff, then: dotnet build && dotnet test'

namespace ES.FX.Zendesk.Abstractions;

// Zendesk's vocabularies are server-defined and can grow; the client deliberately keeps string-typed
// members/parameters (an enum would make deserialization throw on a new server value) and ships the
// well-known values as constants instead, so consumers get discoverability without runtime fragility.

/// <summary>The well-known values of a ticket's <c>status</c>.</summary>
public static class ZendeskTicketStatuses
{
    public const string New = "new";
    public const string Open = "open";
    public const string Pending = "pending";
    public const string Hold = "hold";
    public const string Solved = "solved";
    public const string Closed = "closed";
}

/// <summary>The well-known values of a ticket's <c>priority</c>.</summary>
public static class ZendeskTicketPriorities
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Urgent = "urgent";
}

/// <summary>The well-known values of a ticket's <c>type</c>.</summary>
public static class ZendeskTicketTypes
{
    public const string Problem = "problem";
    public const string Incident = "incident";
    public const string Question = "question";
    public const string Task = "task";
}

/// <summary>The well-known user roles.</summary>
public static class ZendeskUserRoles
{
    public const string EndUser = "end-user";
    public const string Agent = "agent";
    public const string Admin = "admin";
}

/// <summary>The well-known user identity types.</summary>
public static class ZendeskIdentityTypes
{
    public const string Email = "email";
    public const string PhoneNumber = "phone_number";
    public const string Twitter = "twitter";
    public const string Facebook = "facebook";
    public const string Google = "google";
    public const string AgentForwarding = "agent_forwarding";
}

/// <summary>The built-in categories a custom ticket status maps to.</summary>
public static class ZendeskStatusCategories
{
    public const string New = "new";
    public const string Open = "open";
    public const string Pending = "pending";
    public const string Hold = "hold";
    public const string Solved = "solved";
}

/// <summary>
///     The well-known sideload names accepted by <c>include</c> parameters. Each endpoint supports a subset —
///     see the operation's documentation.
/// </summary>
public static class ZendeskSideloads
{
    public const string Users = "users";
    public const string Groups = "groups";
    public const string Organizations = "organizations";
    public const string Identities = "identities";
    public const string CommentCount = "comment_count";
    public const string Sections = "sections";
    public const string Categories = "categories";
}

/// <summary>The sort directions accepted by <c>sortOrder</c> parameters.</summary>
public static class ZendeskSortOrders
{
    public const string Ascending = "asc";
    public const string Descending = "desc";
}

/// <summary>The well-known <c>sortBy</c> fields of the ticket search API.</summary>
public static class ZendeskTicketSortFields
{
    public const string CreatedAt = "created_at";
    public const string UpdatedAt = "updated_at";
    public const string Priority = "priority";
    public const string Status = "status";
    public const string TicketType = "ticket_type";
}

/// <summary>
///     The comment body representations accepted by the <c>bodyFormat</c> parameter of
///     <see cref="IZendeskTicketsApi.GetCommentsAsync" /> (a client-side projection, not a wire value).
/// </summary>
public static class ZendeskCommentBodyFormats
{
    /// <summary>Plain text only (the default — roughly half the payload of both).</summary>
    public const string Plain = "plain";

    /// <summary>Rich/HTML markup only.</summary>
    public const string Rich = "rich";

    /// <summary>Both representations.</summary>
    public const string Both = "both";
}

/// <summary>The well-known OAuth scopes for <c>ZendeskOAuthOptions.Scope</c> (space-separate to combine).</summary>
public static class ZendeskOAuthScopes
{
    public const string Read = "read";
    public const string Write = "write";
}

/// <summary>The terminal and transient states of an async job (<c>ZendeskJobStatus.Status</c>).</summary>
public static class ZendeskJobStatusValues
{
    public const string Queued = "queued";
    public const string Working = "working";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Killed = "killed";
}

/// <summary>The states of an organization merge (<c>ZendeskOrganizationMerge.Status</c>).</summary>
public static class ZendeskOrganizationMergeStatuses
{
    public const string New = "new";
    public const string InProgress = "in progress";
    public const string Error = "error";
    public const string Complete = "complete";
}
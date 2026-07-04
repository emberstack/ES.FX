namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     A typed client for the Zendesk Support REST API, organized by resource area to mirror the Zendesk API
///     structure.
/// </summary>
public interface IZendeskClient
{
    /// <summary>Operations against the Zendesk <c>users</c> resource.</summary>
    IZendeskUsersApi Users { get; }

    /// <summary>Operations against the Zendesk <c>tickets</c> resource.</summary>
    IZendeskTicketsApi Tickets { get; }

    /// <summary>Operations against the Zendesk <c>ticket_forms</c> resource.</summary>
    IZendeskFormsApi Forms { get; }

    /// <summary>Operations against the Zendesk <c>organizations</c> resource.</summary>
    IZendeskOrganizationsApi Organizations { get; }

    /// <summary>Operations against the Zendesk <c>groups</c> resource.</summary>
    IZendeskGroupsApi Groups { get; }

    /// <summary>Operations against the Zendesk Help Center <c>articles</c> (knowledge base).</summary>
    IZendeskArticlesApi Articles { get; }

    /// <summary>Operations against the Zendesk <c>ticket_fields</c> definitions.</summary>
    IZendeskTicketFieldsApi TicketFields { get; }

    /// <summary>Operations against the Zendesk <c>macros</c> resource.</summary>
    IZendeskMacrosApi Macros { get; }

    /// <summary>Operations against Zendesk <c>attachments</c> (authenticated content download).</summary>
    IZendeskAttachmentsApi Attachments { get; }

    /// <summary>Operations against the unified <c>search</c> API (count, cursor-based export).</summary>
    IZendeskSearchApi Search { get; }

    /// <summary>Operations against the Zendesk <c>views</c> resource (saved ticket filters).</summary>
    IZendeskViewsApi Views { get; }

    /// <summary>Operations against the Zendesk <c>brands</c> resource.</summary>
    IZendeskBrandsApi Brands { get; }

    /// <summary>Operations against the Zendesk <c>custom_statuses</c> resource.</summary>
    IZendeskCustomStatusesApi CustomStatuses { get; }

    /// <summary>Operations against the Zendesk <c>job_statuses</c> resource (async bulk-job polling).</summary>
    IZendeskJobStatusesApi JobStatuses { get; }

    /// <summary>Operations against the Zendesk <c>tags</c> resource (account-wide tag usage).</summary>
    IZendeskTagsApi Tags { get; }

    /// <summary>Operations against the Zendesk <c>suspended_tickets</c> resource.</summary>
    IZendeskSuspendedTicketsApi SuspendedTickets { get; }

    /// <summary>Operations against the Zendesk <c>uploads</c> resource (attachment uploads for comments).</summary>
    IZendeskUploadsApi Uploads { get; }
}
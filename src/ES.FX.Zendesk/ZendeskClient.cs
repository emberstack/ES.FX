using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Articles;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.Brands;
using ES.FX.Zendesk.CustomStatuses;
using ES.FX.Zendesk.Forms;
using ES.FX.Zendesk.Groups;
using ES.FX.Zendesk.JobStatuses;
using ES.FX.Zendesk.Macros;
using ES.FX.Zendesk.Organizations;
using ES.FX.Zendesk.Search;
using ES.FX.Zendesk.SuspendedTickets;
using ES.FX.Zendesk.Tags;
using ES.FX.Zendesk.TicketFields;
using ES.FX.Zendesk.Tickets;
using ES.FX.Zendesk.Uploads;
using ES.FX.Zendesk.Users;
using ES.FX.Zendesk.Views;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk;

/// <summary>
///     Default <see cref="IZendeskClient" /> implementation. Registered as a typed <see cref="HttpClient" />
///     whose base address targets <c>https://{subdomain}.zendesk.com/api/v2/</c> and whose authorization header is
///     applied by <see cref="Authentication.ZendeskAuthenticationDelegatingHandler" />.
/// </summary>
internal sealed class ZendeskClient(HttpClient httpClient, ILoggerFactory loggerFactory) : IZendeskClient
{
    /// <inheritdoc />
    public IZendeskUsersApi Users { get; } =
        new ZendeskUsersApi(httpClient, loggerFactory.CreateLogger<ZendeskUsersApi>());

    /// <inheritdoc />
    public IZendeskTicketsApi Tickets { get; } =
        new ZendeskTicketsApi(httpClient, loggerFactory.CreateLogger<ZendeskTicketsApi>());

    /// <inheritdoc />
    public IZendeskFormsApi Forms { get; } =
        new ZendeskFormsApi(httpClient, loggerFactory.CreateLogger<ZendeskFormsApi>());

    /// <inheritdoc />
    public IZendeskOrganizationsApi Organizations { get; } =
        new ZendeskOrganizationsApi(httpClient, loggerFactory.CreateLogger<ZendeskOrganizationsApi>());

    /// <inheritdoc />
    public IZendeskGroupsApi Groups { get; } =
        new ZendeskGroupsApi(httpClient, loggerFactory.CreateLogger<ZendeskGroupsApi>());

    /// <inheritdoc />
    public IZendeskArticlesApi Articles { get; } =
        new ZendeskArticlesApi(httpClient, loggerFactory.CreateLogger<ZendeskArticlesApi>());

    /// <inheritdoc />
    public IZendeskTicketFieldsApi TicketFields { get; } =
        new ZendeskTicketFieldsApi(httpClient, loggerFactory.CreateLogger<ZendeskTicketFieldsApi>());

    /// <inheritdoc />
    public IZendeskMacrosApi Macros { get; } =
        new ZendeskMacrosApi(httpClient, loggerFactory.CreateLogger<ZendeskMacrosApi>());

    /// <inheritdoc />
    public IZendeskAttachmentsApi Attachments { get; } =
        new ZendeskAttachmentsApi(httpClient, loggerFactory.CreateLogger<ZendeskAttachmentsApi>());

    /// <inheritdoc />
    public IZendeskSearchApi Search { get; } =
        new ZendeskSearchApi(httpClient, loggerFactory.CreateLogger<ZendeskSearchApi>());

    /// <inheritdoc />
    public IZendeskViewsApi Views { get; } =
        new ZendeskViewsApi(httpClient, loggerFactory.CreateLogger<ZendeskViewsApi>());

    /// <inheritdoc />
    public IZendeskBrandsApi Brands { get; } =
        new ZendeskBrandsApi(httpClient, loggerFactory.CreateLogger<ZendeskBrandsApi>());

    /// <inheritdoc />
    public IZendeskCustomStatusesApi CustomStatuses { get; } =
        new ZendeskCustomStatusesApi(httpClient, loggerFactory.CreateLogger<ZendeskCustomStatusesApi>());

    /// <inheritdoc />
    public IZendeskJobStatusesApi JobStatuses { get; } =
        new ZendeskJobStatusesApi(httpClient, loggerFactory.CreateLogger<ZendeskJobStatusesApi>());

    /// <inheritdoc />
    public IZendeskTagsApi Tags { get; } =
        new ZendeskTagsApi(httpClient, loggerFactory.CreateLogger<ZendeskTagsApi>());

    /// <inheritdoc />
    public IZendeskSuspendedTicketsApi SuspendedTickets { get; } =
        new ZendeskSuspendedTicketsApi(httpClient, loggerFactory.CreateLogger<ZendeskSuspendedTicketsApi>());

    /// <inheritdoc />
    public IZendeskUploadsApi Uploads { get; } =
        new ZendeskUploadsApi(httpClient, loggerFactory.CreateLogger<ZendeskUploadsApi>());
}
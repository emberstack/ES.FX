using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The output layout of a view (max 10 columns).</summary>
public sealed record ZendeskViewOutput
{
    /// <summary>Up to 10 column tokens for the view's displayed columns.</summary>
    [Description(
        "Up to 10 column tokens for the view's displayed columns (UI title may differ from the token): " +
        "assigned, assignee, due_date, group, nice_id (=ID), updated, updated_assignee, updated_requester, " +
        "updated_by_type, organization, priority, created (=Requested), requester, locale_id, satisfaction_score, " +
        "solved, status, description (=Subject), submitter, ticket_form, type, brand, custom_status_id " +
        "(=Ticket status). For a custom field use its numeric custom-field id.")]
    [JsonPropertyName("columns")]
    public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>A single column token or custom-field id to group by.</summary>
    [Description(
        "A single column token (same vocabulary as columns) or a custom-field numeric id to group by. " +
        "Not supported for grouping: description (Subject), submitter, and custom_status_id.")]
    [JsonPropertyName("group_by")]
    public string? GroupBy { get; init; }

    /// <summary>Group sort direction: "asc" or "desc".</summary>
    [Description("Sort direction: \"asc\" or \"desc\".")]
    [JsonPropertyName("group_order")]
    public string? GroupOrder { get; init; }

    /// <summary>A single column token or custom-field id to sort by.</summary>
    [Description(
        "A single column token (same vocabulary as columns) or a custom-field numeric id to sort by. " +
        "Not supported for sorting: description (Subject), submitter, and custom_status_id.")]
    [JsonPropertyName("sort_by")]
    public string? SortBy { get; init; }

    /// <summary>Sort direction: "asc" or "desc".</summary>
    [Description("Sort direction: \"asc\" or \"desc\".")]
    [JsonPropertyName("sort_order")]
    public string? SortOrder { get; init; }
}
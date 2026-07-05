namespace ES.FX.Zendesk.Tests;

/// <summary>
///     Pins the well-known-value constants to the exact wire strings Zendesk documents — a typo or rename in
///     the constants would otherwise silently send an invalid value.
/// </summary>
public class ZendeskKnownValuesTests
{
    [Fact]
    public void Ticket_Vocabulary_Matches_The_Wire_Values()
    {
        Assert.Equal(["new", "open", "pending", "hold", "solved", "closed"],
        [
            ZendeskTicketStatuses.New, ZendeskTicketStatuses.Open, ZendeskTicketStatuses.Pending,
            ZendeskTicketStatuses.Hold, ZendeskTicketStatuses.Solved, ZendeskTicketStatuses.Closed
        ]);
        Assert.Equal(["low", "normal", "high", "urgent"],
        [
            ZendeskTicketPriorities.Low, ZendeskTicketPriorities.Normal, ZendeskTicketPriorities.High,
            ZendeskTicketPriorities.Urgent
        ]);
        Assert.Equal(["problem", "incident", "question", "task"],
        [
            ZendeskTicketTypes.Problem, ZendeskTicketTypes.Incident, ZendeskTicketTypes.Question,
            ZendeskTicketTypes.Task
        ]);
        Assert.Equal(["created_at", "updated_at", "priority", "status", "ticket_type"],
        [
            ZendeskTicketSortFields.CreatedAt, ZendeskTicketSortFields.UpdatedAt, ZendeskTicketSortFields.Priority,
            ZendeskTicketSortFields.Status, ZendeskTicketSortFields.TicketType
        ]);
    }

    [Fact]
    public void People_And_Status_Vocabulary_Matches_The_Wire_Values()
    {
        Assert.Equal(["end-user", "agent", "admin"],
            [ZendeskUserRoles.EndUser, ZendeskUserRoles.Agent, ZendeskUserRoles.Admin]);
        Assert.Equal(["email", "phone_number", "twitter", "facebook", "google", "agent_forwarding"],
        [
            ZendeskIdentityTypes.Email, ZendeskIdentityTypes.PhoneNumber, ZendeskIdentityTypes.Twitter,
            ZendeskIdentityTypes.Facebook, ZendeskIdentityTypes.Google, ZendeskIdentityTypes.AgentForwarding
        ]);
        Assert.Equal(["new", "open", "pending", "hold", "solved"],
        [
            ZendeskStatusCategories.New, ZendeskStatusCategories.Open, ZendeskStatusCategories.Pending,
            ZendeskStatusCategories.Hold, ZendeskStatusCategories.Solved
        ]);
    }

    [Fact]
    public void Request_And_Job_Vocabulary_Matches_The_Wire_Values()
    {
        Assert.Equal(["users", "groups", "organizations", "identities", "comment_count", "sections", "categories"],
        [
            ZendeskSideloads.Users, ZendeskSideloads.Groups, ZendeskSideloads.Organizations,
            ZendeskSideloads.Identities, ZendeskSideloads.CommentCount, ZendeskSideloads.Sections,
            ZendeskSideloads.Categories
        ]);
        Assert.Equal(["asc", "desc"], [ZendeskSortOrders.Ascending, ZendeskSortOrders.Descending]);
        Assert.Equal(["plain", "rich", "both"],
        [
            ZendeskCommentBodyFormats.Plain, ZendeskCommentBodyFormats.Rich, ZendeskCommentBodyFormats.Both
        ]);
        Assert.Equal(["read", "write"], [ZendeskOAuthScopes.Read, ZendeskOAuthScopes.Write]);
        Assert.Equal(["queued", "working", "completed", "failed", "killed"],
        [
            ZendeskJobStatusValues.Queued, ZendeskJobStatusValues.Working, ZendeskJobStatusValues.Completed,
            ZendeskJobStatusValues.Failed, ZendeskJobStatusValues.Killed
        ]);
        Assert.Equal(["new", "in progress", "error", "complete"],
        [
            ZendeskOrganizationMergeStatuses.New, ZendeskOrganizationMergeStatuses.InProgress,
            ZendeskOrganizationMergeStatuses.Error, ZendeskOrganizationMergeStatuses.Complete
        ]);
    }
}
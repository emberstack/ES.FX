using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     Unit coverage for the area-derivation helper and the fail-closed area gate. The end-to-end registration
///     outcomes (which classes are actually registered for a given <c>Mcp:Tools:Areas</c>) are covered by
///     <see cref="Hosting.McpAreaGatingTests" />; these tests pin the pure logic, including the exact
///     unknown-area failure message.
/// </summary>
public class ZendeskToolAreaTests
{
    [Theory]
    [InlineData("tickets_get", "tickets")]
    [InlineData("tickets_search_export", "tickets")]
    [InlineData("tickets_comments_make_private", "tickets")]
    [InlineData("ticket_fields_options_create_or_update", "ticket_fields")]
    [InlineData("custom_statuses_list", "custom_statuses")]
    [InlineData("job_statuses_get_many", "job_statuses")]
    [InlineData("suspended_tickets_recover_many", "suspended_tickets")]
    [InlineData("organizations_merges_get", "organizations")]
    [InlineData("search_count", "search")]
    [InlineData("users_me_get", "users")]
    public void OfToolName_Derives_The_Area(string toolName, string expectedArea) =>
        Assert.Equal(expectedArea, ZendeskToolArea.OfToolName(toolName));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OfToolName_Rejects_Blank_Names(string toolName) =>
        Assert.Throws<ArgumentException>(() => ZendeskToolArea.OfToolName(toolName));

    [Fact]
    public void OfType_Returns_The_Homogeneous_Area_Of_A_Tool_Class()
    {
        Assert.Equal("tickets", ZendeskToolArea.OfType(typeof(ZendeskTicketTools)));
        Assert.Equal("tickets", ZendeskToolArea.OfType(typeof(ZendeskTicketWriteTools)));
        Assert.Equal("search", ZendeskToolArea.OfType(typeof(ZendeskSearchTools)));
        Assert.Equal("ticket_fields", ZendeskToolArea.OfType(typeof(ZendeskTicketFieldTools)));
        Assert.Equal("uploads", ZendeskToolArea.OfType(typeof(ZendeskUploadWriteTools)));
    }

    [Fact]
    public void Gate_Empty_Configuration_Admits_Every_Area()
    {
        var gate = ZendeskToolAreaGate.FromConfiguration([], typeof(Program).Assembly);

        Assert.False(gate.IsActive);
        Assert.True(gate.Allows<ZendeskTicketTools>());
        Assert.True(gate.Allows<ZendeskUserWriteTools>());
        Assert.True(gate.Allows<ZendeskSearchTools>());
    }

    [Fact]
    public void Gate_Null_Configuration_Admits_Every_Area()
    {
        var gate = ZendeskToolAreaGate.FromConfiguration(null, typeof(Program).Assembly);

        Assert.False(gate.IsActive);
        Assert.True(gate.Allows<ZendeskTicketTools>());
    }

    [Fact]
    public void Gate_Selected_Area_Admits_Only_That_Area_Case_Insensitively()
    {
        var gate = ZendeskToolAreaGate.FromConfiguration(["Tickets"], typeof(Program).Assembly);

        Assert.True(gate.IsActive);
        Assert.True(gate.Allows<ZendeskTicketTools>());
        Assert.True(gate.Allows<ZendeskTicketWriteTools>());
        Assert.False(gate.Allows<ZendeskUserTools>());
        Assert.False(gate.Allows<ZendeskSearchTools>());
    }

    [Fact]
    public void Gate_Unknown_Area_Fails_Closed_With_A_Message_Listing_Valid_Areas()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ZendeskToolAreaGate.FromConfiguration(["tickets", "tikets"], typeof(Program).Assembly));

        // Names the offender and lists the valid areas so the operator can self-correct.
        Assert.Contains("tikets", exception.Message);
        Assert.Contains("Valid areas are", exception.Message);
        Assert.Contains("tickets", exception.Message);
        Assert.Contains("ticket_fields", exception.Message);
        // The valid list must be exactly the real tool areas — no phantom entries, no missing ones.
        foreach (var area in new[]
                 {
                     "articles", "attachments", "brands", "custom_statuses", "forms", "groups", "job_statuses",
                     "macros", "organizations", "search", "suspended_tickets", "tags", "ticket_fields", "tickets",
                     "uploads", "users", "views"
                 })
            Assert.Contains(area, exception.Message);
    }

    [Fact]
    public void Gate_Blank_Entries_Are_Ignored()
    {
        // Whitespace-only entries are dropped, not treated as an unknown area.
        var gate = ZendeskToolAreaGate.FromConfiguration(["tickets", "  ", ""], typeof(Program).Assembly);

        Assert.True(gate.IsActive);
        Assert.True(gate.Allows<ZendeskTicketTools>());
        Assert.False(gate.Allows<ZendeskUserTools>());
    }

    [Fact]
    public void Gate_Present_But_Entirely_Blank_Configuration_Fails_Closed()
    {
        // A configured-but-all-blank list (e.g. a fat-fingered empty env var) is a misconfiguration: fail
        // closed rather than silently exposing the full surface. Distinct from an absent/empty list (= all).
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ZendeskToolAreaGate.FromConfiguration(["", "  "], typeof(Program).Assembly));

        Assert.Contains("no non-blank area names", exception.Message);
    }
}
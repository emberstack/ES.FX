using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     The bulk dry-run digest: instead of echoing up to 100 write models verbatim, a <c>*_many</c> dry run
///     reports {action, target, count, items:[{index, identity, changed field names}]} with long values
///     truncated. Single-entity dry runs are untouched — they keep the verbatim echo.
/// </summary>
public class ZendeskDryRunResultTests
{
    private static JsonElement Serialize(ZendeskDryRunResult result) => JsonSerializer.SerializeToElement(result);

    [Fact]
    public void ForBulk_Digests_Write_Models_To_Identity_Plus_Changed_Field_Names()
    {
        var result = ZendeskDryRunResult.ForBulk("update 2 tickets in bulk", "update", "tickets",
        [
            new ZendeskTicketWrite { Id = 35436, Status = "solved", Subject = new string('s', 150) },
            new ZendeskTicketWrite { ExternalId = "ext-9", Priority = "high", Tags = ["vip"] }
        ]);

        var payload = Serialize(result);
        Assert.Equal("dry_run", payload.GetProperty("status").GetString());
        Assert.False(payload.GetProperty("executed").GetBoolean());
        Assert.Equal("Dry run — no changes were made. This call would update 2 tickets in bulk.",
            payload.GetProperty("description").GetString());

        var digest = payload.GetProperty("request");
        Assert.Equal("update", digest.GetProperty("action").GetString());
        Assert.Equal("tickets", digest.GetProperty("target").GetString());
        Assert.Equal(2, digest.GetProperty("count").GetInt32());

        var first = digest.GetProperty("items")[0];
        Assert.Equal(0, first.GetProperty("index").GetInt32());
        Assert.Equal(35436, first.GetProperty("id").GetInt64());
        // Long identity VALUES are truncated — the digest identifies, it does not replay.
        Assert.Equal(new string('s', 100) + "…", first.GetProperty("subject").GetString());
        var firstFields = first.GetProperty("fields").EnumerateArray().Select(field => field.GetString()).ToArray();
        Assert.Contains("subject", firstFields);
        Assert.Contains("status", firstFields);
        Assert.DoesNotContain("id", firstFields); // the id identifies the row; it is not a change
        Assert.DoesNotContain("priority", firstFields); // null (unsent) fields are not "changes"

        var second = digest.GetProperty("items")[1];
        Assert.Equal(1, second.GetProperty("index").GetInt32());
        Assert.Equal("ext-9", second.GetProperty("external_id").GetString());
        Assert.False(second.TryGetProperty("id", out _));
        var secondFields = second.GetProperty("fields").EnumerateArray().Select(field => field.GetString())
            .ToArray();
        Assert.Equal(new[] { "external_id", "priority", "tags" }, secondFields.Order().ToArray());
    }

    [Fact]
    public void ForBulk_Digests_Primitive_Id_Lists()
    {
        var result = ZendeskDryRunResult.ForBulk("soft-delete 3 tickets", "delete", "tickets", [1L, 2L, 3L]);

        var digest = Serialize(result).GetProperty("request");
        Assert.Equal(3, digest.GetProperty("count").GetInt32());
        var items = digest.GetProperty("items");
        Assert.Equal(0, items[0].GetProperty("index").GetInt32());
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal(3, items[2].GetProperty("id").GetInt64());
        Assert.False(items[0].TryGetProperty("fields", out _));
    }

    [Fact]
    public void ForBulk_Handles_An_Empty_Item_List()
    {
        var result = ZendeskDryRunResult.ForBulk("update 0 tickets", "update", "tickets", []);

        var digest = Serialize(result).GetProperty("request");
        Assert.Equal(0, digest.GetProperty("count").GetInt32());
        Assert.Equal(0, digest.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void Single_Entity_Dry_Runs_Keep_The_Verbatim_Echo()
    {
        // The long-standing contract, restated: Request is the caller's own object, unmodified.
        var request = new { subject = "hello" };
        var result = new ZendeskDryRunResult { Description = "Dry run.", Request = request };

        Assert.Same(request, result.Request);
    }
}
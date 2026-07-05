using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskWriteAcknowledgementTests
{
    [Fact]
    public void Serializes_Without_Id_Fields_When_None_Are_Set()
    {
        var json = JsonSerializer.Serialize(new ZendeskWriteAcknowledgement { Description = "done" });

        Assert.Equal("""{"status":"completed","description":"done"}""", json);
    }

    [Fact]
    public void Carries_A_Structured_Id_Alongside_The_Prose()
    {
        var json = JsonSerializer.Serialize(new ZendeskWriteAcknowledgement
        {
            Description = "Zendesk accepted the request to soft-delete ticket 35436.",
            Id = 35436
        });

        Assert.Contains("\"id\":35436", json);
        Assert.DoesNotContain("\"ids\"", json);
    }

    [Fact]
    public void Carries_Structured_Ids_For_Bulk_Acknowledgements()
    {
        var json = JsonSerializer.Serialize(new ZendeskWriteAcknowledgement
        {
            Description = "Zendesk accepted the request to recover 3 suspended tickets.",
            Ids = [1, 2, 3]
        });

        Assert.Contains("\"ids\":[1,2,3]", json);
        Assert.DoesNotContain("\"id\":", json);
    }
}
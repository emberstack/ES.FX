namespace Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

public class SimpleUser
{
    public Guid Id { get; set; }

    public required string Username { get; set; }
}
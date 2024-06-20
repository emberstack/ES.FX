#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer.Migrations;

/// <inheritdoc />
public partial class V1 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "SimpleUsers",
            table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_SimpleUsers", x => x.Id); });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "SimpleUsers");
    }
}
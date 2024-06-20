#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Migrations;

/// <inheritdoc />
public partial class V1 : Migration
{
    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "TestUsers");
    }

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "TestUsers",
            table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_TestUsers", x => x.Id); });
    }
}
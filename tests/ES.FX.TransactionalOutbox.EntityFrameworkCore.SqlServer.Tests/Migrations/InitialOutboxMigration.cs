#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests.Migrations;

/// <inheritdoc />
public partial class InitialOutboxMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "__Outboxes",
            table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                AddedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                Lock = table.Column<Guid>("uniqueidentifier", nullable: true),
                DeliveryDelayedUntil = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK___Outboxes", x => x.Id); });

        migrationBuilder.CreateTable(
            "__OutboxMessages",
            table => new
            {
                Id = table.Column<long>("bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OutboxId = table.Column<Guid>("uniqueidentifier", nullable: false),
                AddedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                Headers = table.Column<string>("nvarchar(max)", nullable: true),
                Payload = table.Column<string>("nvarchar(max)", nullable: false),
                PayloadType = table.Column<string>("nvarchar(max)", nullable: false),
                ActivityId = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: true),
                DeliveryAttempts = table.Column<int>("int", nullable: false),
                DeliveryFirstAttemptedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
                DeliveryLastAttemptedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
                DeliveryNotBefore = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
                DeliveryNotAfter = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK___OutboxMessages", x => x.Id); });

        migrationBuilder.CreateTable(
            "Orders",
            table => new
            {
                Id = table.Column<int>("int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OrderNumber = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: false),
                Amount = table.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Orders", x => x.Id); });

        migrationBuilder.CreateIndex(
            "IX___Outboxes_AddedAt",
            "__Outboxes",
            "AddedAt");

        migrationBuilder.CreateIndex(
            "IX___Outboxes_DeliveryDelayedUntil",
            "__Outboxes",
            "DeliveryDelayedUntil");

        migrationBuilder.CreateIndex(
            "IX___Outboxes_Lock",
            "__Outboxes",
            "Lock");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "__Outboxes");

        migrationBuilder.DropTable(
            "__OutboxMessages");

        migrationBuilder.DropTable(
            "Orders");
    }
}
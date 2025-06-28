#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql.Tests.Migrations;

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
                Id = table.Column<Guid>("uuid", nullable: false),
                AddedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                Lock = table.Column<Guid>("uuid", nullable: true),
                DeliveryDelayedUntil = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                RowVersion = table.Column<byte[]>("bytea", rowVersion: true, nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK___Outboxes", x => x.Id); });

        migrationBuilder.CreateTable(
            "__OutboxMessages",
            table => new
            {
                Id = table.Column<long>("bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OutboxId = table.Column<Guid>("uuid", nullable: false),
                AddedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                Headers = table.Column<string>("text", nullable: true),
                Payload = table.Column<string>("text", nullable: false),
                PayloadType = table.Column<string>("text", nullable: false),
                ActivityId = table.Column<string>("character varying(128)", maxLength: 128, nullable: true),
                DeliveryAttempts = table.Column<int>("integer", nullable: false),
                DeliveryFirstAttemptedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                DeliveryLastAttemptedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                DeliveryNotBefore = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                DeliveryNotAfter = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                RowVersion = table.Column<byte[]>("bytea", rowVersion: true, nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK___OutboxMessages", x => x.Id); });

        migrationBuilder.CreateTable(
            "Orders",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderNumber = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Amount = table.Column<decimal>("numeric(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
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
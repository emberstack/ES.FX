using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "__Outboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Lock = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryDelayedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___Outboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "__OutboxMessageFaults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false),
                    DeliveryFirstAttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryLastAttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryLastAttemptError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeliveryNotBefore = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryNotAfter = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FaultedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___OutboxMessageFaults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "__OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false),
                    DeliveryMaxAttempts = table.Column<int>(type: "int", nullable: true),
                    DeliveryFirstAttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryLastAttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryLastAttemptError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeliveryNotBefore = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryNotAfter = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryAttemptDelay = table.Column<int>(type: "int", nullable: false),
                    DeliveryAttemptDelayIsExponential = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SimpleUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimpleUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX___Outboxes_AddedAt",
                table: "__Outboxes",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX___Outboxes_DeliveryDelayedUntil",
                table: "__Outboxes",
                column: "DeliveryDelayedUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__Outboxes");

            migrationBuilder.DropTable(
                name: "__OutboxMessageFaults");

            migrationBuilder.DropTable(
                name: "__OutboxMessages");

            migrationBuilder.DropTable(
                name: "SimpleUsers");
        }
    }
}

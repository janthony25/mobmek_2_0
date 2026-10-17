using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddXeroSyncPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultSalesAccountCode",
                table: "XeroConnections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XeroInvoiceId",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XeroLastError",
                table: "Invoices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "XeroLastSyncedUtc",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XeroSyncStatus",
                table: "Invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSynced");

            migrationBuilder.AddColumn<string>(
                name: "XeroContactId",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "XeroSyncItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XeroSyncItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XeroSyncItems_InvoiceId",
                table: "XeroSyncItems",
                column: "InvoiceId",
                unique: true,
                filter: "\"Action\" = 'PushInvoice'");

            migrationBuilder.CreateIndex(
                name: "IX_XeroSyncItems_NextAttemptUtc",
                table: "XeroSyncItems",
                column: "NextAttemptUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XeroSyncItems");

            migrationBuilder.DropColumn(
                name: "DefaultSalesAccountCode",
                table: "XeroConnections");

            migrationBuilder.DropColumn(
                name: "XeroInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "XeroLastError",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "XeroLastSyncedUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "XeroSyncStatus",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "XeroContactId",
                table: "Customers");
        }
    }
}

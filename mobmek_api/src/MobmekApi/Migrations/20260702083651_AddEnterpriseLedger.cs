using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayeeId",
                table: "CashTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SplitGroupId",
                table: "CashTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CashTransactions",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "Cleared");

            migrationBuilder.AddColumn<DateOnly>(
                name: "LockDate",
                table: "CashFlowSettings",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashFlowAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Changes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultGstTreatment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payees_TransactionCategories_DefaultCategoryId",
                        column: x => x.DefaultCategoryId,
                        principalTable: "TransactionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CategorizationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    MatchField = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AmountMin = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AmountMax = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SetCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetGstTreatment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SetPayeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorizationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategorizationRules_Payees_SetPayeeId",
                        column: x => x.SetPayeeId,
                        principalTable: "Payees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CategorizationRules_TransactionCategories_SetCategoryId",
                        column: x => x.SetCategoryId,
                        principalTable: "TransactionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_PayeeId",
                table: "CashTransactions",
                column: "PayeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_SplitGroupId",
                table: "CashTransactions",
                column: "SplitGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_Status",
                table: "CashTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowAuditLogs_CreatedAtUtc",
                table: "CashFlowAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowAuditLogs_EntityType_EntityId",
                table: "CashFlowAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_SetCategoryId",
                table: "CategorizationRules",
                column: "SetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_SetPayeeId",
                table: "CategorizationRules",
                column: "SetPayeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payees_DefaultCategoryId",
                table: "Payees",
                column: "DefaultCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Payees_Name",
                table: "Payees",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransactions_Payees_PayeeId",
                table: "CashTransactions",
                column: "PayeeId",
                principalTable: "Payees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransactions_Payees_PayeeId",
                table: "CashTransactions");

            migrationBuilder.DropTable(
                name: "CashFlowAuditLogs");

            migrationBuilder.DropTable(
                name: "CategorizationRules");

            migrationBuilder.DropTable(
                name: "Payees");

            migrationBuilder.DropIndex(
                name: "IX_CashTransactions_PayeeId",
                table: "CashTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CashTransactions_SplitGroupId",
                table: "CashTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CashTransactions_Status",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "PayeeId",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "SplitGroupId",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "LockDate",
                table: "CashFlowSettings");
        }
    }
}

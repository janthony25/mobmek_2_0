using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaymentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashAmount",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DatePaid",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CardAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CashAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DatePaid",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Invoices");
        }
    }
}

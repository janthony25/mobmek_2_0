using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedByToBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "TransactionCategories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "TransactionCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "TransactionAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "TransactionAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "ReminderTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "ReminderTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Reminders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Reminders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "RecurringTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "RecurringTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "PlannedTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "PlannedTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Payees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Payees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "PasswordChangeCodes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "PasswordChangeCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "OutboundEmails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "OutboundEmails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "LoginAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "LoginAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Labour",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Labour",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "JobServices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "JobServices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "JobServiceLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "JobServiceLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "JobItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "JobItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "InvoiceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "InvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "GstSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "GstSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "EmploymentTypes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "EmploymentTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "EmployeeTitles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "EmployeeTitles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "EmailSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "EmailSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "EmailConfirmationCodes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "EmailConfirmationCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CategorizationRules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CategorizationRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CashTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CashTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CashFlowSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CashFlowSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CashFlowAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CashFlowAuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CashAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CashAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Cars",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Cars",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CarModels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CarModels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "CarMakes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "CarMakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "BusinessDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "BusinessDetails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "Appointments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Appointments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "TransactionCategories");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "TransactionCategories");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "TransactionAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "TransactionAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "ReminderTemplates");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ReminderTemplates");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "PlannedTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PlannedTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Payees");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Payees");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "PasswordChangeCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PasswordChangeCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "OutboundEmails");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "OutboundEmails");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "LoginAttempts");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "LoginAttempts");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Labour");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Labour");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "JobServices");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "JobServices");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "JobServiceLines");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "JobServiceLines");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "JobItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "JobItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "GstSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "GstSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "EmploymentTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "EmploymentTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "EmployeeTitles");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "EmployeeTitles");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "EmailSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "EmailSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "EmailConfirmationCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "EmailConfirmationCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CategorizationRules");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CategorizationRules");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CashFlowSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CashFlowSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CashFlowAuditLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CashFlowAuditLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CashAccounts");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CarModels");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CarModels");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "CarMakes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CarMakes");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Appointments");
        }
    }
}

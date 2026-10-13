using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddShowOnInvoiceToPartialPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnInvoice",
                table: "InvoicePartialPayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowOnInvoice",
                table: "InvoicePartialPayments");
        }
    }
}

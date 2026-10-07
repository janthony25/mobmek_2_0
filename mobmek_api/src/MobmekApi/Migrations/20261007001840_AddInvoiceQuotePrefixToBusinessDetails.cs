using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceQuotePrefixToBusinessDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoicePrefix",
                table: "BusinessDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "INV");

            migrationBuilder.AddColumn<string>(
                name: "QuotePrefix",
                table: "BusinessDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "QUO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoicePrefix",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "QuotePrefix",
                table: "BusinessDetails");
        }
    }
}

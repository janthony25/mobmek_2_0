using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSequenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "BusinessDetails",
                newName: "BusinessPhone");

            migrationBuilder.RenameColumn(
                name: "Abn",
                table: "BusinessDetails",
                newName: "GstNumber");

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BankDetails",
                table: "BusinessDetails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telephone",
                table: "BusinessDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "BusinessDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "BusinessDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BankDetails",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "Telephone",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "BusinessDetails");

            migrationBuilder.RenameColumn(
                name: "BusinessPhone",
                table: "BusinessDetails",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "GstNumber",
                table: "BusinessDetails",
                newName: "Abn");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class MakeInvoiceSequenceNumberIndexPartial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_DocumentType_SequenceNumber",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DocumentType_SequenceNumber",
                table: "Invoices",
                columns: new[] { "DocumentType", "SequenceNumber" },
                unique: true,
                filter: "\"SequenceNumber\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_DocumentType_SequenceNumber",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DocumentType_SequenceNumber",
                table: "Invoices",
                columns: new[] { "DocumentType", "SequenceNumber" },
                unique: true);
        }
    }
}

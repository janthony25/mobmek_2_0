using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Jobs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Jobs");
        }
    }
}

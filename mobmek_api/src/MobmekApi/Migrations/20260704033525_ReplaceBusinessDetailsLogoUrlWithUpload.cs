using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBusinessDetailsLogoUrlWithUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "BusinessDetails");

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "BusinessDetails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoFileName",
                table: "BusinessDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoStorageKey",
                table: "BusinessDetails",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "LogoFileName",
                table: "BusinessDetails");

            migrationBuilder.DropColumn(
                name: "LogoStorageKey",
                table: "BusinessDetails");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "BusinessDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}

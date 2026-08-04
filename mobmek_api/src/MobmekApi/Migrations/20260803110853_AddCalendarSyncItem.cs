using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSyncItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarSyncItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    GoogleEventId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_CalendarSyncItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncItems_AppointmentId",
                table: "CalendarSyncItems",
                column: "AppointmentId",
                unique: true,
                filter: "\"Action\" = 'Upsert'");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSyncItems_NextAttemptUtc",
                table: "CalendarSyncItems",
                column: "NextAttemptUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarSyncItems");
        }
    }
}

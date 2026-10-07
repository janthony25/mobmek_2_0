using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobmekApi.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderAndAppointmentEmailLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "OutboundEmails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReminderId",
                table: "OutboundEmails",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundEmails_AppointmentId",
                table: "OutboundEmails",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundEmails_ReminderId",
                table: "OutboundEmails",
                column: "ReminderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundEmails_Appointments_AppointmentId",
                table: "OutboundEmails",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboundEmails_Reminders_ReminderId",
                table: "OutboundEmails",
                column: "ReminderId",
                principalTable: "Reminders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutboundEmails_Appointments_AppointmentId",
                table: "OutboundEmails");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboundEmails_Reminders_ReminderId",
                table: "OutboundEmails");

            migrationBuilder.DropIndex(
                name: "IX_OutboundEmails_AppointmentId",
                table: "OutboundEmails");

            migrationBuilder.DropIndex(
                name: "IX_OutboundEmails_ReminderId",
                table: "OutboundEmails");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "OutboundEmails");

            migrationBuilder.DropColumn(
                name: "ReminderId",
                table: "OutboundEmails");
        }
    }
}

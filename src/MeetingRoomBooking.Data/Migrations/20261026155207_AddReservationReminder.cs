using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingRoomBooking.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentUtc",
                table: "Reservations",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Status_ReminderSentUtc_StartUtc",
                table: "Reservations",
                columns: new[] { "Status", "ReminderSentUtc", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_Status_ReminderSentUtc_StartUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ReminderSentUtc",
                table: "Reservations");
        }
    }
}

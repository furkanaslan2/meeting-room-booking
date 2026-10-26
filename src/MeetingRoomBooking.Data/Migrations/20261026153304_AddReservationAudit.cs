using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingRoomBooking.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReservationId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OccurredUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BeforeJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AfterJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationAuditEntries_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationAuditEntries_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAuditEntries_ActorUserId",
                table: "ReservationAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAuditEntries_ReservationId_OccurredUtc",
                table: "ReservationAuditEntries",
                columns: new[] { "ReservationId", "OccurredUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationAuditEntries");
        }
    }
}

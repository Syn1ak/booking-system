using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Bookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentBookingId",
                table: "Slots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Slots",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_CurrentBookingId",
                table: "Slots",
                column: "CurrentBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SlotId",
                table: "Bookings",
                column: "SlotId",
                unique: true,
                filter: "[CancelledAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId",
                table: "Bookings",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Slots_Bookings_CurrentBookingId",
                table: "Slots",
                column: "CurrentBookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Slots_Bookings_CurrentBookingId",
                table: "Slots");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Slots_CurrentBookingId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "CurrentBookingId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Slots");
        }
    }
}

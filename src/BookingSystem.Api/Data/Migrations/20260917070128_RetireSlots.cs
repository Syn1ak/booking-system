using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetireSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Slots_RoomId_StartsAtUtc",
                table: "Slots");

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAtUtc",
                table: "Slots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Slots_RoomId_StartsAtUtc",
                table: "Slots",
                columns: new[] { "RoomId", "StartsAtUtc" },
                unique: true,
                filter: "[RetiredAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Slots_RoomId_StartsAtUtc",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "RetiredAtUtc",
                table: "Slots");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_RoomId_StartsAtUtc",
                table: "Slots",
                columns: new[] { "RoomId", "StartsAtUtc" },
                unique: true);
        }
    }
}

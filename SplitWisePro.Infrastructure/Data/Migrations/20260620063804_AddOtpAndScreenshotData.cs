using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitWisePro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpAndScreenshotData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenshotUrl",
                table: "TripExpenses");

            migrationBuilder.AddColumn<int>(
                name: "OtpAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetOtpExpiry",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetOtpHash",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreenshotData",
                table: "TripExpenses",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetOtpExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetOtpHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ScreenshotData",
                table: "TripExpenses");

            migrationBuilder.AddColumn<string>(
                name: "ScreenshotUrl",
                table: "TripExpenses",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}

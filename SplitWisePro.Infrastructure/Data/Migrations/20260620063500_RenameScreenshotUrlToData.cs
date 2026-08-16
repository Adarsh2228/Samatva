using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitWisePro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameScreenshotUrlToData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the column from ScreenshotUrl to ScreenshotData and change type to TEXT
            migrationBuilder.RenameColumn(
                name: "ScreenshotUrl",
                table: "TripExpenses",
                newName: "ScreenshotData");

            migrationBuilder.AlterColumn<string>(
                name: "ScreenshotData",
                table: "TripExpenses",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ScreenshotData",
                table: "TripExpenses",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "ScreenshotData",
                table: "TripExpenses",
                newName: "ScreenshotUrl");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitWisePro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteCodeToGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Groups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteCodeExpiresAt",
                table: "Groups",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "InviteCodeExpiresAt",
                table: "Groups");
        }
    }
}

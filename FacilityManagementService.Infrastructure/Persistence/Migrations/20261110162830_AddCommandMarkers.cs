using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacilityManagementService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandMarkers",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "nvarchar(360)", maxLength: 360, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandMarkers", x => new { x.Scope, x.Key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandMarkers_ExpiresAt",
                table: "CommandMarkers",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandMarkers");
        }
    }
}

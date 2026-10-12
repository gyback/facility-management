using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacilityManagementService.IntegrationTests.Probe.Migrations
{
    /// <inheritdoc />
    public partial class InitialProbe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Probes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Probes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Probes_Name",
                table: "Probes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Probes");
        }
    }
}

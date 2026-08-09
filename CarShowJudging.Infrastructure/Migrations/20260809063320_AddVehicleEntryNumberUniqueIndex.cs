using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleEntryNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_EntryNumber",
                table: "Vehicles",
                column: "EntryNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_EntryNumber",
                table: "Vehicles");
        }
    }
}

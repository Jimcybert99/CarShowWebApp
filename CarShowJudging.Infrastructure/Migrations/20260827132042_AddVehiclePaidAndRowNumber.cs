using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclePaidAndRowNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Paid",
                table: "Vehicles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "Vehicles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Paid",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "Vehicles");
        }
    }
}

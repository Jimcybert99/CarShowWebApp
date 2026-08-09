using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameScoreCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SuperCoolnessFactor",
                table: "Scores",
                newName: "Presentation");

            migrationBuilder.RenameColumn(
                name: "ShowAppeal",
                table: "Scores",
                newName: "Exterior");

            migrationBuilder.RenameColumn(
                name: "PaintAndBody",
                table: "Scores",
                newName: "EngineBay");

            migrationBuilder.RenameColumn(
                name: "Condition",
                table: "Scores",
                newName: "Craftsmanship");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Presentation",
                table: "Scores",
                newName: "SuperCoolnessFactor");

            migrationBuilder.RenameColumn(
                name: "Exterior",
                table: "Scores",
                newName: "ShowAppeal");

            migrationBuilder.RenameColumn(
                name: "EngineBay",
                table: "Scores",
                newName: "PaintAndBody");

            migrationBuilder.RenameColumn(
                name: "Craftsmanship",
                table: "Scores",
                newName: "Condition");
        }
    }
}

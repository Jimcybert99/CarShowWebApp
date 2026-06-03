using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SiteNotes_PageContext",
                table: "SiteNotes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SiteNotes_PageContext",
                table: "SiteNotes",
                column: "PageContext");
        }
    }
}

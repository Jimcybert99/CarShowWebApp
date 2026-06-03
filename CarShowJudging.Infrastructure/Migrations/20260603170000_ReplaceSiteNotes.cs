using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSiteNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VehicleNotes");

            migrationBuilder.CreateTable(
                name: "SiteNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PageContext = table.Column<string>(type: "TEXT", nullable: true),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentNoteId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    IsImportant = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteNotes_SiteNotes_ParentNoteId",
                        column: x => x.ParentNoteId,
                        principalTable: "SiteNotes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SiteNotes_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteNotes_PageContext",
                table: "SiteNotes",
                column: "PageContext");

            migrationBuilder.CreateIndex(
                name: "IX_SiteNotes_ParentNoteId",
                table: "SiteNotes",
                column: "ParentNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteNotes_VehicleId",
                table: "SiteNotes",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SiteNotes");

            migrationBuilder.CreateTable(
                name: "VehicleNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleNotes_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleNotes_VehicleId",
                table: "VehicleNotes",
                column: "VehicleId");
        }
    }
}

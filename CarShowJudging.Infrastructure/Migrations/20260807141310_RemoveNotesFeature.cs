using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotesFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteNotes");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNote",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationNote",
                table: "Vehicles");

            migrationBuilder.CreateTable(
                name: "SiteNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentNoteId = table.Column<int>(type: "INTEGER", nullable: true),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuthorDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsImportant = table.Column<bool>(type: "INTEGER", nullable: false),
                    PageContext = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "IX_SiteNotes_ParentNoteId",
                table: "SiteNotes",
                column: "ParentNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteNotes_VehicleId",
                table: "SiteNotes",
                column: "VehicleId");
        }
    }
}

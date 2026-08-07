using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShowJudging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowUserDeletionKeepEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Scores_AspNetUsers_JudgeId",
                table: "Scores");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_AspNetUsers_RegisteredById",
                table: "Vehicles");

            migrationBuilder.AlterColumn<string>(
                name: "RegisteredById",
                table: "Vehicles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_Scores_AspNetUsers_JudgeId",
                table: "Scores",
                column: "JudgeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_AspNetUsers_RegisteredById",
                table: "Vehicles",
                column: "RegisteredById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Scores_AspNetUsers_JudgeId",
                table: "Scores");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_AspNetUsers_RegisteredById",
                table: "Vehicles");

            migrationBuilder.AlterColumn<string>(
                name: "RegisteredById",
                table: "Vehicles",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Scores_AspNetUsers_JudgeId",
                table: "Scores",
                column: "JudgeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_AspNetUsers_RegisteredById",
                table: "Vehicles",
                column: "RegisteredById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

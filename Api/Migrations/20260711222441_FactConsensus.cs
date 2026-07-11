using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class FactConsensus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Confidence",
                table: "Facts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "FactEndorsements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FactId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactEndorsements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactEndorsements_Facts_FactId",
                        column: x => x.FactId,
                        principalTable: "Facts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FactEndorsements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactEndorsements_FactId",
                table: "FactEndorsements",
                column: "FactId");

            migrationBuilder.CreateIndex(
                name: "IX_FactEndorsements_UserId",
                table: "FactEndorsements",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactEndorsements");

            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "Facts");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class Phase15_EmotionSentiment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmotionIntensity",
                table: "Facts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sentiment",
                table: "Facts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmotionKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Word = table.Column<string>(type: "TEXT", nullable: false),
                    Sentiment = table.Column<string>(type: "TEXT", nullable: false),
                    Intensity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmotionKeywords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmotionKeywords_Word",
                table: "EmotionKeywords",
                column: "Word",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmotionKeywords");

            migrationBuilder.DropColumn(
                name: "EmotionIntensity",
                table: "Facts");

            migrationBuilder.DropColumn(
                name: "Sentiment",
                table: "Facts");
        }
    }
}

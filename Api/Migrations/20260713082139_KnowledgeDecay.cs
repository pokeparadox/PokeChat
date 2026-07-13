using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeDecay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessCount",
                table: "WordDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastAccessed",
                table: "WordDefinitions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessCount",
                table: "LearnedResponseRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastAccessed",
                table: "LearnedResponseRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessCount",
                table: "Facts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastAccessed",
                table: "Facts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessCount",
                table: "WordDefinitions");

            migrationBuilder.DropColumn(
                name: "LastAccessed",
                table: "WordDefinitions");

            migrationBuilder.DropColumn(
                name: "AccessCount",
                table: "LearnedResponseRules");

            migrationBuilder.DropColumn(
                name: "LastAccessed",
                table: "LearnedResponseRules");

            migrationBuilder.DropColumn(
                name: "AccessCount",
                table: "Facts");

            migrationBuilder.DropColumn(
                name: "LastAccessed",
                table: "Facts");
        }
    }
}

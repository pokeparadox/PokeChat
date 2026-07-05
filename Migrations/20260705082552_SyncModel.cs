using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Persona",
                table: "ResponseRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Persona",
                table: "Greetings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Persona",
                table: "BotResponses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ErrorKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false),
                    Suggestion = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    IsLearned = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorKnowledgeEntries", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorKnowledgeEntries");

            migrationBuilder.DropColumn(
                name: "Persona",
                table: "ResponseRules");

            migrationBuilder.DropColumn(
                name: "Persona",
                table: "Greetings");

            migrationBuilder.DropColumn(
                name: "Persona",
                table: "BotResponses");
        }
    }
}

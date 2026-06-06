using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class Phase22_ConversationMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseCategory",
                table: "Conversations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FactsLearned = table.Column<int>(type: "INTEGER", nullable: false),
                    DominantSentiment = table.Column<string>(type: "TEXT", nullable: true),
                    SentimentTrend = table.Column<string>(type: "TEXT", nullable: true),
                    TopicsDiscussed = table.Column<int>(type: "INTEGER", nullable: false),
                    BotResponseStats = table.Column<string>(type: "TEXT", nullable: true),
                    AvgResponseLength = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionLength = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<string>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResponseEffectiveness",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    AvgSessionLengthAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    UsedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FollowUpRate = table.Column<double>(type: "REAL", nullable: false),
                    LastUsed = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseEffectiveness", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_UserId",
                table: "ConversationMetrics",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMetrics");

            migrationBuilder.DropTable(
                name: "ResponseEffectiveness");

            migrationBuilder.DropColumn(
                name: "ResponseCategory",
                table: "Conversations");
        }
    }
}

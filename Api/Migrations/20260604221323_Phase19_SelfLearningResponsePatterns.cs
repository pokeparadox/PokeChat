using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class Phase19_SelfLearningResponsePatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearnedResponseRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    InputType = table.Column<string>(type: "TEXT", nullable: false),
                    LearnedFromUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnedResponseRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnedResponseRules_Users_LearnedFromUserId",
                        column: x => x.LearnedFromUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResponseFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLearnedRule = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", nullable: false),
                    CorrectionText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResponseFeedbacks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearnedResponseRules_LearnedFromUserId",
                table: "LearnedResponseRules",
                column: "LearnedFromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseFeedbacks_UserId",
                table: "ResponseFeedbacks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearnedResponseRules");

            migrationBuilder.DropTable(
                name: "ResponseFeedbacks");
        }
    }
}

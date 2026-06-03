using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class Phase16_TemporalKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MentionedAt",
                table: "Facts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeContext",
                table: "Facts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemporalExpressions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Expression = table.Column<string>(type: "TEXT", nullable: false),
                    DaysOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRange = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporalExpressions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemporalExpressions_Expression",
                table: "TemporalExpressions",
                column: "Expression",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemporalExpressions");

            migrationBuilder.DropColumn(
                name: "MentionedAt",
                table: "Facts");

            migrationBuilder.DropColumn(
                name: "TimeContext",
                table: "Facts");
        }
    }
}

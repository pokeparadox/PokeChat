using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeChat.Migrations
{
    /// <inheritdoc />
    public partial class AddContextStateJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextStateJson",
                table: "ConversationSessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextStateJson",
                table: "ConversationSessions");
        }
    }
}

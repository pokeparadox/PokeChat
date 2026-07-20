using Microsoft.EntityFrameworkCore.Migrations;

namespace PokeChat.Api.Migrations
{
    public partial class AddIsTrainedToTaskList : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE TaskLists ADD COLUMN IsTrained BOOLEAN DEFAULT 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE TaskLists DROP COLUMN IsTrained;");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PokeChat.Api.Migrations
{
    public partial class AddRecursiveTaskFramework : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE TaskLists (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GoalDescription TEXT NOT NULL,
                    ContextTags TEXT,
                    SuccessRating REAL DEFAULT 0.0,
                    Version INTEGER DEFAULT 1,
                    IsTemplate BOOLEAN DEFAULT 1,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LastUsedAt DATETIME
                );

                CREATE TABLE Tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TaskListId INTEGER NOT NULL,
                    SequenceOrder INTEGER NOT NULL,
                    Type TEXT CHECK(Type IN ('ToolCall', 'SubPlan', 'Reasoning')) NOT NULL,
                    Payload TEXT,
                    Status TEXT CHECK(Status IN ('Pending', 'Running', 'Completed', 'Failed', 'Skipped')) DEFAULT 'Pending',
                    Result TEXT,
                    ErrorMessage TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (TaskListId) REFERENCES TaskLists(Id) ON DELETE CASCADE
                );

                CREATE INDEX idx_tasklists_tags ON TaskLists(ContextTags);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS Tasks;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TaskLists;");
        }
    }
}

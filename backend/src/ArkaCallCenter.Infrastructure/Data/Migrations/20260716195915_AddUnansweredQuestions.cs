using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkaCallCenter.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnansweredQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnansweredQuestionsJson",
                table: "CallSessions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnansweredQuestionsJson",
                table: "CallSessions");
        }
    }
}

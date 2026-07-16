using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkaCallCenter.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnswerAccuracy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnswerAccuracyPercent",
                table: "SmartPhones",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerAccuracyPercent",
                table: "SmartPhones");
        }
    }
}

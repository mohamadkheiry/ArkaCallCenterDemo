using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkaCallCenter.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoAndReception : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DemoLabel",
                table: "Users",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsDemo",
                table: "Users",
                column: "IsDemo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsDemo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DemoLabel",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Users");
        }
    }
}

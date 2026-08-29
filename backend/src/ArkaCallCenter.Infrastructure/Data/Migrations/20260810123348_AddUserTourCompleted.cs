using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkaCallCenter.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTourCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedTour",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // کاربران موجود قبلاً وارد سامانه شده‌اند؛ انتشار این قابلیت نباید تور را
            // دوباره برای آن‌ها باز کند. کاربران جدید با مقدار false ساخته می‌شوند.
            migrationBuilder.Sql("UPDATE Users SET HasCompletedTour = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCompletedTour",
                table: "Users");
        }
    }
}

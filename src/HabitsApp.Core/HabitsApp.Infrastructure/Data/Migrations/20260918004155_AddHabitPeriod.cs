using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitsApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Period",
                table: "Habits",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Period",
                table: "Habits");
        }
    }
}

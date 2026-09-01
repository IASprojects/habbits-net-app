using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitsApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitHourKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HabitLogs_HabitId_PeriodKey",
                table: "HabitLogs");

            migrationBuilder.AddColumn<string>(
                name: "HourKey",
                table: "HabitLogs",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE \"HabitLogs\" SET \"HourKey\" = to_char(\"CompletedAtUtc\", 'YYYY-MM-DD\"T\"HH24');");

            migrationBuilder.CreateIndex(
                name: "IX_HabitLogs_HabitId_HourKey",
                table: "HabitLogs",
                columns: new[] { "HabitId", "HourKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HabitLogs_HabitId_HourKey",
                table: "HabitLogs");

            migrationBuilder.DropColumn(
                name: "HourKey",
                table: "HabitLogs");

            migrationBuilder.CreateIndex(
                name: "IX_HabitLogs_HabitId_PeriodKey",
                table: "HabitLogs",
                columns: new[] { "HabitId", "PeriodKey" },
                unique: true);
        }
    }
}

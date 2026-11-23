using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McqExam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAttemptDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Device",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Device",
                table: "ExamAttempts");
        }
    }
}

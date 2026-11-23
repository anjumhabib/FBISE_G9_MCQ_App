using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace McqExam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdGlobalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    AdsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PublisherId = table.Column<string>(type: "TEXT", maxLength: 23, nullable: true),
                    AutoAdsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PreviewPlaceholderMode = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AdsTxtContent = table.Column<string>(type: "TEXT", nullable: true),
                    InFeedInterval = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 6),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdGlobalSettings", x => x.Id);
                    table.CheckConstraint("CK_AdGlobalSettings_Singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "AdPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PageKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SlotKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    AdFormat = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AdSlotId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    MinHeightPx = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HideOnMobile = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    HideOnDesktop = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdPlacements", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AdGlobalSettings",
                columns: new[] { "Id", "AdsTxtContent", "InFeedInterval", "PublisherId", "UpdatedAt" },
                values: new object[] { 1, null, 6, null, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "AdPlacements",
                columns: new[] { "Id", "AdFormat", "AdSlotId", "Description", "DisplayName", "IsEnabled", "MinHeightPx", "PageKey", "SlotKey", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "ResponsiveBanner", null, "Between the subject cards and the SLO assessment overview.", "Home mid-page banner", true, 100, "PublicHome", "home_mid_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "ResponsiveBanner", null, "Below the subject introduction and above the chapter breakdown.", "Subject guide top banner", true, 100, "PublicSubjectGuide", "subject_top_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "ResponsiveBanner", null, "Below the study tips and sample quiz links.", "Subject guide bottom banner", true, 100, "PublicSubjectGuide", "subject_bottom_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "ResponsiveBanner", null, "Between the exam strategy and the Bloom’s level examples.", "SLO guide mid-page banner", true, 100, "PublicSloGuide", "slo_mid_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "ResponsiveBanner", null, "Below the sample quiz content, separate from quiz controls.", "Sample quiz bottom banner", true, 100, "SampleQuiz", "sample_quiz_bottom", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "ResponsiveBanner", null, "Below the test setup card, separate from the Start Test button.", "Test setup bottom banner", true, 100, "TestSetup", "setup_bottom_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "AdPlacements",
                columns: new[] { "Id", "AdFormat", "AdSlotId", "Description", "DisplayName", "HideOnMobile", "IsEnabled", "MinHeightPx", "PageKey", "SlotKey", "UpdatedAt" },
                values: new object[] { 7, "SidebarRectangle", null, "Below the Question Navigator on desktop, separate from exam controls and answers.", "Exam desktop sidebar", true, true, 250, "ExamRunner", "exam_desktop_sidebar", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "AdPlacements",
                columns: new[] { "Id", "AdFormat", "AdSlotId", "Description", "DisplayName", "IsEnabled", "MinHeightPx", "PageKey", "SlotKey", "UpdatedAt" },
                values: new object[,]
                {
                    { 8, "ResponsiveBanner", null, "Below the Bloom’s level summary and above the question review.", "Answer review top banner", true, 100, "AnswerReview", "review_top_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, "InFeedNative", null, "Between complete reviewed question cards at the configured interval.", "Answer review in-feed", true, 250, "AnswerReview", "review_infeed", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, "ResponsiveBanner", null, "Between the score trend chart and attempt history.", "History mid-page banner", true, 100, "HistoryReport", "history_mid_banner", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdPlacements_SlotKey",
                table: "AdPlacements",
                column: "SlotKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdGlobalSettings");

            migrationBuilder.DropTable(
                name: "AdPlacements");
        }
    }
}

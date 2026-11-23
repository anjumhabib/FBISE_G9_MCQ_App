using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

internal static class AdModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        var settings = model.Entity<AdGlobalSettings>();
        settings.ToTable(table => table.HasCheckConstraint("CK_AdGlobalSettings_Singleton",
            $"Id = {AdGlobalSettings.SingletonId}"));
        settings.Property(x => x.Id).ValueGeneratedNever();
        settings.Property(x => x.AdsEnabled).HasDefaultValue(false);
        settings.Property(x => x.PublisherId).HasMaxLength(23);
        settings.Property(x => x.AutoAdsEnabled).HasDefaultValue(false);
        settings.Property(x => x.PreviewPlaceholderMode).HasDefaultValue(false);
        settings.Property(x => x.InFeedInterval).HasDefaultValue(6);
        settings.Property(x => x.UpdatedAt)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        settings.HasData(new AdGlobalSettings { UpdatedAt = DateTime.UnixEpoch });

        var placement = model.Entity<AdPlacement>();
        placement.Property(x => x.PageKey).HasConversion<string>().HasMaxLength(32);
        placement.Property(x => x.SlotKey).HasMaxLength(64);
        placement.HasIndex(x => x.SlotKey).IsUnique();
        placement.Property(x => x.DisplayName).HasMaxLength(100);
        placement.Property(x => x.Description).HasMaxLength(250);
        placement.Property(x => x.AdFormat).HasConversion<string>().HasMaxLength(32);
        placement.Property(x => x.AdSlotId).HasMaxLength(32);
        placement.Property(x => x.IsEnabled).HasDefaultValue(true);
        placement.Property(x => x.HideOnMobile).HasDefaultValue(false);
        placement.Property(x => x.HideOnDesktop).HasDefaultValue(false);
        placement.Property(x => x.UpdatedAt)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        placement.HasData(
            Slot(1, AdPageKey.PublicHome, "home_mid_banner", "Home mid-page banner",
                "Between the subject cards and the SLO assessment overview."),
            Slot(2, AdPageKey.PublicSubjectGuide, "subject_top_banner", "Subject guide top banner",
                "Below the subject introduction and above the chapter breakdown."),
            Slot(3, AdPageKey.PublicSubjectGuide, "subject_bottom_banner", "Subject guide bottom banner",
                "Below the study tips and sample quiz links."),
            Slot(4, AdPageKey.PublicSloGuide, "slo_mid_banner", "SLO guide mid-page banner",
                "Between the exam strategy and the Bloom’s level examples."),
            Slot(5, AdPageKey.SampleQuiz, "sample_quiz_bottom", "Sample quiz bottom banner",
                "Below the sample quiz content, separate from quiz controls."),
            Slot(6, AdPageKey.TestSetup, "setup_bottom_banner", "Test setup bottom banner",
                "Below the test setup card, separate from the Start Test button."),
            Slot(7, AdPageKey.ExamRunner, "exam_desktop_sidebar", "Exam desktop sidebar",
                "Below the Question Navigator on desktop, separate from exam controls and answers.",
                AdFormat.SidebarRectangle, 250, hideOnMobile: true),
            Slot(8, AdPageKey.AnswerReview, "review_top_banner", "Answer review top banner",
                "Below the Bloom’s level summary and above the question review."),
            Slot(9, AdPageKey.AnswerReview, "review_infeed", "Answer review in-feed",
                "Between complete reviewed question cards at the configured interval.", AdFormat.InFeedNative, 250),
            Slot(10, AdPageKey.HistoryReport, "history_mid_banner", "History mid-page banner",
                "Between the score trend chart and attempt history."));
    }

    private static AdPlacement Slot(int id, AdPageKey page, string key, string name, string description,
        AdFormat format = AdFormat.ResponsiveBanner, int minHeight = 100, bool hideOnMobile = false) => new()
    {
        Id = id, PageKey = page, SlotKey = key, DisplayName = name, Description = description,
        AdFormat = format, MinHeightPx = minHeight, HideOnMobile = hideOnMobile, UpdatedAt = DateTime.UnixEpoch
    };
}

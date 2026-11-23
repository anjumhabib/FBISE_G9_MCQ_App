namespace McqExam.Domain;

public enum AdPageKey
{
    PublicHome, PublicSubjectGuide, PublicSloGuide, SampleQuiz, TestSetup, ExamRunner, AnswerReview, HistoryReport
}

public enum AdFormat { ResponsiveBanner, SidebarRectangle, InFeedNative }

public sealed class AdGlobalSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public bool AdsEnabled { get; set; }
    public string? PublisherId { get; set; }
    public bool AutoAdsEnabled { get; set; }
    public bool PreviewPlaceholderMode { get; set; }
    public string? AdsTxtContent { get; set; }
    public int InFeedInterval { get; set; } = 6;
    public DateTime UpdatedAt { get; set; }
}

public sealed class AdPlacement
{
    public int Id { get; set; }
    public AdPageKey PageKey { get; set; }
    public string SlotKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public AdFormat AdFormat { get; set; }
    public string? AdSlotId { get; set; }
    public int MinHeightPx { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool HideOnMobile { get; set; }
    public bool HideOnDesktop { get; set; }
    public DateTime UpdatedAt { get; set; }
}

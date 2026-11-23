using McqExam.Domain;

namespace McqExam.Application;

public static class AdPolicy
{
    public static bool CanRender(AdGlobalConfig global, AdPlacementConfig slot, bool allowPreview) =>
        global.AdsEnabled && slot.IsEnabled && !(slot.HideOnDesktop && (slot.HideOnMobile || slot.PageKey == AdPageKey.ExamRunner)) &&
        (global.PreviewPlaceholderMode ? allowPreview :
            AdSenseIdentifiers.ValidPublisher(global.PublisherId) && AdSenseIdentifiers.ValidSlot(slot.AdSlotId));

    public static bool CanLoadAutoAds(AdGlobalConfig global, AdPageKey page) =>
        global.AdsEnabled && !global.PreviewPlaceholderMode && global.AutoAdsEnabled &&
        page != AdPageKey.ExamRunner && AdSenseIdentifiers.ValidPublisher(global.PublisherId);

    public static AdPageKey? PageForPath(string path)
    {
        if (path == "/") return AdPageKey.PublicHome;

        return path.TrimEnd('/').ToLowerInvariant().Split('/') switch
        {
            ["", "subjects"] => AdPageKey.PublicSubjectGuide,
            ["", "subjects", var slug] when IsSubjectSlug(slug) => AdPageKey.PublicSubjectGuide,
            ["", "fbise-slo-guide"] => AdPageKey.PublicSloGuide,
            ["", "sample-quiz", var slug] when IsSubjectSlug(slug) => AdPageKey.SampleQuiz,
            ["", "setup"] => AdPageKey.TestSetup,
            ["", "exam", var id] when Guid.TryParse(id, out _) => AdPageKey.ExamRunner,
            ["", "review", var id] when Guid.TryParse(id, out _) => AdPageKey.AnswerReview,
            ["", "history"] => AdPageKey.HistoryReport,
            _ => null
        };
    }

    private static bool IsSubjectSlug(string slug) =>
        slug is "biology" or "chemistry" or "physics" or "mathematics" or "computer-science";
}

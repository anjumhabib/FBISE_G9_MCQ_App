using McqExam.Domain;

namespace McqExam.Application;

public sealed record AdGlobalConfig(int Id, bool AdsEnabled, string? PublisherId, bool AutoAdsEnabled,
    bool PreviewPlaceholderMode, string? AdsTxtContent, int InFeedInterval, DateTime UpdatedAt);

public sealed record AdPlacementConfig(int Id, AdPageKey PageKey, string SlotKey, string DisplayName,
    string Description, AdFormat AdFormat, string? AdSlotId, int MinHeightPx, bool IsEnabled,
    bool HideOnMobile, bool HideOnDesktop, DateTime UpdatedAt);

public sealed record AdConfiguration(AdGlobalConfig Global, IReadOnlyList<AdPlacementConfig> Placements);

public interface IAdConfigService
{
    Task<AdConfiguration> GetConfiguration(CancellationToken ct = default);
    Task<bool> ShouldRenderSlot(string slotKey, bool allowPreview = false, CancellationToken ct = default);
    Task<bool> ShouldLoadAdSenseScript(string currentPath, CancellationToken ct = default);
    Task Invalidate();
}

public static class AdSenseIdentifiers
{
    public static bool ValidPublisher(string? publisherId) =>
        publisherId is { Length: 23 } && publisherId.StartsWith("ca-pub-", StringComparison.Ordinal) &&
        publisherId[7..].All(char.IsAsciiDigit);

    public static bool ValidSlot(string? slotId) => slotId is { Length: > 0 and <= 32 } && slotId.All(char.IsAsciiDigit);
}

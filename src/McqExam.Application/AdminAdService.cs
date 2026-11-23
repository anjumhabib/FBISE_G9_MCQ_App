using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using McqExam.Domain;

namespace McqExam.Application;

public sealed record UpdateAdGlobal(
    [property: JsonRequired] bool AdsEnabled,
    [StringLength(23)] string? PublisherId,
    [property: JsonRequired] bool AutoAdsEnabled,
    [property: JsonRequired] bool PreviewPlaceholderMode,
    [Range(5, 10)] int InFeedInterval,
    string? AdsTxtContent);

public sealed record UpdateAdPlacement(
    [property: JsonRequired] bool IsEnabled,
    [StringLength(32)] string? AdSlotId,
    [property: JsonRequired] bool HideOnMobile,
    [property: JsonRequired] bool HideOnDesktop,
    [Range(1, int.MaxValue)] int MinHeightPx);

public sealed record ToggleAdPage([property: JsonRequired] bool IsEnabled);
public sealed record AdminAdPage(AdPageKey PageKey, IReadOnlyList<AdPlacementConfig> Placements);
public sealed record AdminAdSettings(AdGlobalConfig Global, IReadOnlyList<AdminAdPage> Pages);

public interface IAdminAdStore
{
    Task<bool> SaveGlobal(UpdateAdGlobal settings, DateTime updatedAt, CancellationToken ct);
    Task<bool> SavePlacement(int id, UpdateAdPlacement settings, DateTime updatedAt, CancellationToken ct);
    Task<bool> TogglePage(AdPageKey page, bool enabled, DateTime updatedAt, CancellationToken ct);
}

public sealed class AdminAdService(IAdConfigService config, IAdminAdStore store, TimeProvider clock)
{
    public async Task<AdminAdSettings> Settings(CancellationToken ct)
    {
        var settings = await config.GetConfiguration(ct);
        return new(settings.Global, settings.Placements.GroupBy(x => x.PageKey)
            .Select(page => new AdminAdPage(page.Key, page.ToArray())).ToArray());
    }

    public async Task SaveGlobal(UpdateAdGlobal request, CancellationToken ct)
    {
        var publisher = string.IsNullOrWhiteSpace(request.PublisherId) ? null : request.PublisherId.Trim();
        if (publisher is not null && !AdSenseIdentifiers.ValidPublisher(publisher))
            throw new RequestFailure("Enter a publisher ID with ca-pub- followed by 16 digits.");

        var text = request.AdsTxtContent;
        if (string.IsNullOrWhiteSpace(text))
            text = publisher is null ? null : $"google.com, {publisher[3..]}, DIRECT, f08c47fec0942fa0\n";

        if (!await store.SaveGlobal(request with { PublisherId = publisher, AdsTxtContent = text },
                clock.GetUtcNow().UtcDateTime, ct))
            throw new RequestFailure("Ad settings not found.", 404);
        await config.Invalidate();
    }

    public async Task SavePlacement(int id, UpdateAdPlacement request, CancellationToken ct)
    {
        var slot = string.IsNullOrWhiteSpace(request.AdSlotId) ? null : request.AdSlotId.Trim();
        if (slot is not null && !AdSenseIdentifiers.ValidSlot(slot))
            throw new RequestFailure("Enter a numeric AdSense slot ID.");

        if (!await store.SavePlacement(id, request with { AdSlotId = slot }, clock.GetUtcNow().UtcDateTime, ct))
            throw new RequestFailure("Ad placement not found.", 404);
        await config.Invalidate();
    }

    public async Task TogglePage(string pageKey, ToggleAdPage request, CancellationToken ct)
    {
        var page = Enum.GetNames<AdPageKey>().FirstOrDefault(name => name.Equals(pageKey, StringComparison.OrdinalIgnoreCase));
        if (page is null || !await store.TogglePage(Enum.Parse<AdPageKey>(page), request.IsEnabled, clock.GetUtcNow().UtcDateTime, ct))
            throw new RequestFailure("Ad page not found.", 404);
        await config.Invalidate();
    }
}

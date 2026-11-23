using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace McqExam.Infrastructure;

public sealed class AdConfigService(IMemoryCache cache, IServiceScopeFactory scopes) : IAdConfigService, IDisposable
{
    private static readonly object CacheKey = new();
    private readonly SemaphoreSlim cacheGate = new(1);

    public async Task<AdConfiguration> GetConfiguration(CancellationToken ct = default)
    {
        if (cache.TryGetValue<AdConfiguration>(CacheKey, out var cached) && cached is not null)
            return cached;

        await cacheGate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(CacheKey, out cached) && cached is not null)
                return cached;

            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExamDb>();
            var global = await db.AdGlobalSettings.AsNoTracking().Where(x => x.Id == AdGlobalSettings.SingletonId)
                .Select(x => new AdGlobalConfig(x.Id, x.AdsEnabled, x.PublisherId, x.AutoAdsEnabled,
                    x.PreviewPlaceholderMode, x.AdsTxtContent, x.InFeedInterval, x.UpdatedAt)).SingleAsync(ct);
            var placements = await db.AdPlacements.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new AdPlacementConfig(x.Id, x.PageKey, x.SlotKey, x.DisplayName, x.Description,
                    x.AdFormat, x.AdSlotId, x.MinHeightPx, x.IsEnabled, x.HideOnMobile, x.HideOnDesktop, x.UpdatedAt))
                .ToArrayAsync(ct);
            return cache.Set(CacheKey, new AdConfiguration(global, Array.AsReadOnly(placements)), TimeSpan.FromMinutes(10));
        }
        finally { cacheGate.Release(); }
    }

    public async Task<bool> ShouldRenderSlot(string slotKey, bool allowPreview = false, CancellationToken ct = default)
    {
        var config = await GetConfiguration(ct);
        var slot = config.Placements.FirstOrDefault(x => string.Equals(x.SlotKey, slotKey, StringComparison.Ordinal));
        return slot is not null && AdPolicy.CanRender(config.Global, slot, allowPreview);
    }

    public async Task<bool> ShouldLoadAdSenseScript(string currentPath, CancellationToken ct = default)
    {
        var page = AdPolicy.PageForPath(currentPath);
        if (page is null)
            return false;

        var config = await GetConfiguration(ct);
        return AdPolicy.CanLoadAutoAds(config.Global, page.Value) ||
            (!config.Global.PreviewPlaceholderMode &&
                config.Placements.Any(slot => slot.PageKey == page && AdPolicy.CanRender(config.Global, slot, allowPreview: false)));
    }

    public async Task Invalidate()
    {
        // Wait for any active load so it cannot repopulate the cache with an older snapshot after invalidation.
        await cacheGate.WaitAsync();
        try { cache.Remove(CacheKey); }
        finally { cacheGate.Release(); }
    }

    public void Dispose() => cacheGate.Dispose();

}

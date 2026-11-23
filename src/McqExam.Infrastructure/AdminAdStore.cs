using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class AdminAdStore(ExamDb db) : IAdminAdStore
{
    public async Task<bool> SaveGlobal(UpdateAdGlobal settings, DateTime updatedAt, CancellationToken ct) =>
        await db.AdGlobalSettings.Where(x => x.Id == AdGlobalSettings.SingletonId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.AdsEnabled, settings.AdsEnabled)
                .SetProperty(x => x.PublisherId, settings.PublisherId)
                .SetProperty(x => x.AutoAdsEnabled, settings.AutoAdsEnabled)
                .SetProperty(x => x.PreviewPlaceholderMode, settings.PreviewPlaceholderMode)
                .SetProperty(x => x.InFeedInterval, settings.InFeedInterval)
                .SetProperty(x => x.AdsTxtContent, settings.AdsTxtContent)
                .SetProperty(x => x.UpdatedAt, updatedAt), ct) > 0;

    public async Task<bool> SavePlacement(int id, UpdateAdPlacement settings, DateTime updatedAt, CancellationToken ct) =>
        await db.AdPlacements.Where(x => x.Id == id)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.IsEnabled, settings.IsEnabled)
                .SetProperty(x => x.AdSlotId, settings.AdSlotId)
                .SetProperty(x => x.HideOnMobile, settings.HideOnMobile)
                .SetProperty(x => x.HideOnDesktop, settings.HideOnDesktop)
                .SetProperty(x => x.MinHeightPx, settings.MinHeightPx)
                .SetProperty(x => x.UpdatedAt, updatedAt), ct) > 0;

    public async Task<bool> TogglePage(AdPageKey page, bool enabled, DateTime updatedAt, CancellationToken ct) =>
        await db.AdPlacements.Where(x => x.PageKey == page)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.IsEnabled, enabled)
                .SetProperty(x => x.UpdatedAt, updatedAt), ct) > 0;
}

using McqExam.Application;
using McqExam.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace McqExam.WebAPI;

public sealed record AdPageSettings(AdGlobalConfig Global, IReadOnlyList<AdPlacementConfig> Slots,
    bool AutoLoad, bool RequireAdminPreview, string? InFeedLayoutKey)
{
    public string? ScriptUrl => Global.PreviewPlaceholderMode ? null :
        $"https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client={Global.PublisherId}";
}

public sealed class AdPageRendering(IAdConfigService ads, IWebHostEnvironment environment, IConfiguration configuration)
{
    private Task<AdPageSettings?>? pageSettings;

    public Task<AdPageSettings?> For(HttpContext context) => pageSettings ??= Load(context);

    private async Task<AdPageSettings?> Load(HttpContext context)
    {
        if (context.Response.StatusCode != StatusCodes.Status200OK ||
            context.Features.Get<IExceptionHandlerFeature>() is not null ||
            context.Features.Get<IStatusCodeReExecuteFeature>() is not null ||
            AdPolicy.PageForPath(context.Request.Path.Value ?? "") is not { } page)
            return null;

        var snapshot = await ads.GetConfiguration(context.RequestAborted);
        var global = snapshot.Global;
        var layoutKey = configuration["Ads:ReviewInFeedLayoutKey"]?.Trim();
        var slots = snapshot.Placements.Where(slot => slot.PageKey == page &&
            AdPolicy.CanRender(global, slot, allowPreview: true) &&
            (global.PreviewPlaceholderMode || slot.AdFormat != AdFormat.InFeedNative || !string.IsNullOrEmpty(layoutKey)))
            .ToArray();
        var autoLoad = AdPolicy.CanLoadAutoAds(global, page);
        if (slots.Length == 0 && !autoLoad)
            return null;

        // The Razor shell usually has no bearer token. The browser verifies production preview access before showing it.
        var requireAdmin = global.PreviewPlaceholderMode && !environment.IsDevelopment() &&
            !context.User.IsInRole(nameof(UserRole.Admin));
        return new(global, slots, autoLoad, requireAdmin, layoutKey);
    }
}

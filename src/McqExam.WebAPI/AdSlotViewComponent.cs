using McqExam.Application;
using McqExam.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace McqExam.WebAPI;

public sealed record AdSlotView(AdPlacementConfig Placement, AdPageSettings Page)
{
    public bool HideOnMobile => Placement.HideOnMobile || Placement.PageKey == AdPageKey.ExamRunner;
}

public sealed class AdSlotViewComponent(AdPageRendering rendering) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string slotKey)
    {
        var page = await rendering.For(HttpContext);
        var slot = page?.Slots.FirstOrDefault(x => x.SlotKey == slotKey);
        if (page is null || slot is null)
            return new HtmlContentViewComponentResult(Microsoft.AspNetCore.Html.HtmlString.Empty);

        var model = new AdSlotView(slot, page);
        return slot.AdFormat == AdFormat.InFeedNative ? View("InFeed", model) : View(model);
    }
}

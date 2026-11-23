using McqExam.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McqExam.WebAPI;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/ads")]
public sealed class AdminAdsController(AdminAdService ads) : ControllerBase
{
    [HttpGet("settings")]
    public Task<AdminAdSettings> Settings(CancellationToken ct) => ads.Settings(ct);

    [HttpPut("global")]
    public async Task<IActionResult> SaveGlobal(UpdateAdGlobal request, CancellationToken ct)
    {
        await ads.SaveGlobal(request, ct);
        return NoContent();
    }

    [HttpPut("placements/{id:int}")]
    public async Task<IActionResult> SavePlacement(int id, UpdateAdPlacement request, CancellationToken ct)
    {
        await ads.SavePlacement(id, request, ct);
        return NoContent();
    }

    [HttpPut("pages/{pageKey}/toggle")]
    public async Task<IActionResult> TogglePage(string pageKey, ToggleAdPage request, CancellationToken ct)
    {
        await ads.TogglePage(pageKey, request, ct);
        return NoContent();
    }
}

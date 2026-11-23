using System.Text;
using McqExam.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McqExam.WebAPI;

public sealed class AdsTxtController(IAdConfigService ads) : ControllerBase
{
    [HttpGet("/ads.txt")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var content = (await ads.GetConfiguration(ct)).Global.AdsTxtContent;
        return string.IsNullOrWhiteSpace(content) ? NotFound() : Content(content, "text/plain", Encoding.UTF8);
    }
}

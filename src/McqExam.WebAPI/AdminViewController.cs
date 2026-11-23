using Microsoft.AspNetCore.Mvc;

namespace McqExam.WebAPI;

[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("admin")]
public sealed class AdminViewController : Controller
{
    // Razor requests open a shell. The bearer-protected API supplies all report data.
    [HttpGet("")]
    public IActionResult Dashboard() => Screen("Dashboard", "Dashboard", "dashboard");

    [HttpGet("students")]
    public IActionResult Students() => Screen("Students", "Students", "students");

    [HttpGet("subjects")]
    public IActionResult Subjects() => Screen("Subjects", "Subjects & Chapters", "subjects");

    [HttpGet("questions")]
    public IActionResult Questions() => Screen("Questions", "Question Analytics", "questions");

    [HttpGet("quality")]
    public IActionResult Quality() => Screen("Quality", "Quality & Duplicates", "quality");

    [HttpGet("visits")]
    public IActionResult Visits() => Screen("Visits", "User Visits", "visits");

    [HttpGet("ads")]
    public IActionResult Ads() => Screen("Ads", "Ad Settings", "ads");

    [HttpGet("attempts/{attemptId:guid}")]
    public IActionResult Attempt(Guid attemptId)
    {
        ViewData["AttemptId"] = attemptId;
        return Screen("Attempt", "Test Review", "attempt");
    }

    private IActionResult Screen(string view, string title, string page)
    {
        ViewData["Title"] = title;
        ViewData["AdminPage"] = page;
        return View(view);
    }
}

using System.Xml.Linq;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Extensions;

namespace McqExam.WebAPI;

public sealed class PublicSiteUrls(IConfiguration configuration)
{
    public string Absolute(HttpRequest request, string path)
    {
        var configured = configuration["PublicSite:BaseUrl"]?.Trim();
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme is ("http" or "https") &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + path;

        return UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, new PathString(path));
    }
}

[AllowAnonymous, ApiExplorerSettings(IgnoreApi = true)]
[TrackVisit(UserVisitEventType.PageView)]
public sealed class PublicStudyController(PublicStudyService study, PublicSiteUrls urls, IConfiguration settings) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        var subjects = await study.Subjects(ct);
        return Page("Home", "FBISE Class 9 Science practice and study guides",
            "Explore FBISE SSC-I science study guides, SLO-based practice and free five-question sample quizzes.", "/",
            new PublicHome(subjects.Count, subjects.Sum(x => x.Chapters.Count), subjects.Sum(x => x.ChaptersWithDiagrams)));
    }

    [HttpGet("/subjects")]
    public IActionResult Subjects() => Page("Subjects", "Class 9 subject study guides",
        "Browse FBISE Class 9 study guides for Biology, Chemistry, Physics, Mathematics and Computer Science.", "/subjects");

    [HttpGet("/subjects/{slug}")]
    public async Task<IActionResult> Subject(string slug, CancellationToken ct)
    {
        var subject = PublicStudyCatalog.Find(slug);
        if (subject is null) return NotFound();
        var chapters = (await study.Subjects(ct)).FirstOrDefault(x => x.Code == subject.Code)?.Chapters ?? [];
        return Page("Subject", $"Class 9 {subject.Name} syllabus and study guide",
            subject.Description, "/subjects/" + subject.Slug, new StudyGuide(subject, chapters, await study.Coverage(subject.Code, ct)), subject.Image);
    }

    [HttpGet("/fbise-slo-guide")]
    public IActionResult SloGuide() => Page("SloGuide", "FBISE SLO and Bloom’s Taxonomy guide",
        "Learn how FBISE Class 9 student learning outcomes, Section A and the practice bank’s four Bloom’s levels fit together.", "/fbise-slo-guide");

    [HttpGet("/sample-quiz/{slug}")]
    public IActionResult Sample(string slug)
    {
        var subject = PublicStudyCatalog.Find(slug);
        return subject is null ? NotFound() : Page("Sample", $"Free Class 9 {subject.Name} sample quiz",
            $"Try five FBISE-style {subject.Name} practice questions and review your answers. No sign-in required.",
            "/sample-quiz/" + subject.Slug, subject, subject.Image);
    }

    [HttpGet("/about")]
    public IActionResult About() => Page("About", "About the Class 9 MCQ Bank",
        "Learn how this independent FBISE Class 9 practice resource organizes and reviews its questions.", "/about");

    [HttpGet("/contact")]
    public IActionResult Contact() => ContactView(new("", "", ""));

    [HttpPost("/contact"), ValidateAntiForgeryToken, EnableRateLimiting("contact")]
    public async Task<IActionResult> SendContact(ContactRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ContactView(request);
        await study.Contact(request, ct);
        TempData["ContactSent"] = true;
        return RedirectToAction(nameof(Contact));
    }

    [HttpGet("/privacy-policy")]
    public IActionResult Privacy() => Page("Privacy", "Privacy policy",
        "Learn how student profiles, exam history, contact messages, cookies and advertising data are used.", "/privacy-policy");

    [HttpGet("/terms")]
    public IActionResult Terms() => Page("Terms", "Terms and educational disclaimer",
        "Read the acceptable-use terms and independent educational disclaimer for the Class 9 MCQ Bank.", "/terms");

    private IActionResult ContactView(ContactRequest request)
    {
        ViewData["SupportEmail"] = settings["PublicSite:SupportEmail"];
        return Page("Contact", "Contact and report a question",
            "Report a Class 9 question or diagram issue, suggest a syllabus update, or send a support inquiry.", "/contact", request);
    }

    private IActionResult Page(string view, string title, string description, string path, object? model = null,
        string image = "/images/public/hero-fbise-science.svg")
    {
        ViewData["PublicPage"] = true;
        ViewData["Title"] = title;
        ViewData["Description"] = description;
        ViewData["Canonical"] = urls.Absolute(Request, path);
        ViewData["SocialImage"] = urls.Absolute(Request, image);
        return View(view, model);
    }
}

[AllowAnonymous, ApiController, EnableRateLimiting("sample-quiz"), Route("api/public/sample-quiz")]
public sealed class SampleQuizController(PublicStudyService study) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<ActionResult<SampleQuiz>> Start(string slug, CancellationToken ct)
    {
        var subject = PublicStudyCatalog.Find(slug);
        if (subject is null) return NotFound();
        return await study.Start(subject.Code, ct);
    }

    [HttpPost("submit")]
    public Task<SampleQuizResult> Submit(SampleQuizSubmission request, CancellationToken ct) => study.Grade(request, ct);
}

[AllowAnonymous, ApiExplorerSettings(IgnoreApi = true)]
public sealed class PublicSeoController(PublicSiteUrls urls) : ControllerBase
{
    [HttpGet("/robots.txt")]
    public IActionResult Robots() => Content(
        "User-agent: *\nAllow: /\nDisallow: /admin\nDisallow: /api/\nDisallow: /exam/\nDisallow: /review/\n" +
        "Disallow: /setup\nDisallow: /history\nSitemap: " + urls.Absolute(Request, "/sitemap.xml") + "\n", "text/plain");

    [HttpGet("/sitemap.xml")]
    public IActionResult Sitemap()
    {
        string[] staticPaths = ["/", "/subjects", "/fbise-slo-guide", "/about", "/contact", "/privacy-policy", "/terms"];
        var paths = staticPaths.Concat(PublicStudyCatalog.Subjects.SelectMany(x =>
            new[] { "/subjects/" + x.Slug, "/sample-quiz/" + x.Slug }));
        XNamespace schema = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var xml = new XElement(schema + "urlset", paths.Select(path =>
            new XElement(schema + "url", new XElement(schema + "loc", urls.Absolute(Request, path)))));
        return Content(xml.ToString(), "application/xml");
    }
}

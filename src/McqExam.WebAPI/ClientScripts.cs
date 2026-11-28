using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.FileProviders;

namespace McqExam.WebAPI;

public sealed class ClientScripts : IDisposable
{
    public PhysicalFileProvider Files { get; }
    public PathString RequestPath { get; }

    public ClientScripts(IWebHostEnvironment environment)
    {
        var directory = Path.Combine(environment.WebRootPath, "js");
        using var revision = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
            revision.AppendData(Encoding.UTF8.GetBytes(relativePath + "\0"));
            using var stream = File.OpenRead(file);
            revision.AppendData(SHA256.HashData(stream));
        }

        RequestPath = new("/assets/js/" + Convert.ToHexString(revision.GetHashAndReset()).ToLowerInvariant());
        Files = new(directory);
    }

    public void Dispose() => Files.Dispose();
}

[HtmlTargetElement("script", Attributes = "app-script")]
public sealed class ClientScriptTagHelper(ClientScripts scripts) : TagHelper
{
    [HtmlAttributeName("app-script")]
    public string Entry { get; set; } = "";

    [HtmlAttributeNotBound, ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var pathBase = ViewContext.HttpContext.Request.PathBase;
        output.Attributes.SetAttribute("src", $"{pathBase}{scripts.RequestPath}/{Entry}");
    }
}

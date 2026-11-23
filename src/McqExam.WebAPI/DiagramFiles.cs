using McqExam.Application;

namespace McqExam.WebAPI;

internal sealed class DiagramFiles(IWebHostEnvironment environment) : IDiagramFiles
{
    private const string DiagramPrefix = "/images/diagrams/";

    public bool Exists(string? imagePath)
    {
        if (imagePath is null || !imagePath.StartsWith(DiagramPrefix, StringComparison.Ordinal))
            return false;

        var fileName = imagePath[DiagramPrefix.Length..];
        if (fileName is "" or "." or ".." || fileName.IndexOfAny(['/', '\\', ':', '?', '#', '%']) >= 0 ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        var file = environment.WebRootFileProvider.GetFileInfo("images/diagrams/" + fileName);
        return file.Exists && !file.IsDirectory;
    }
}

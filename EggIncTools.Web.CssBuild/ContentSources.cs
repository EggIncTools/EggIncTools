namespace EggIncTools.Web.CssBuild;

public static class ContentSources {
    private static readonly string[] SkippedSegments = ["bin", "obj", "Tests"];

    public static IReadOnlyList<string> MarkupProjects { get; } = [
        "EggIncTools.Web",
        "EggIncTools.Shell",
    ];

    public static IEnumerable<string> Enumerate(string repoRoot) {
        foreach (var project in MarkupProjects) {
            var dir = Path.Combine(repoRoot, project);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in EnumerateMarkup(dir)) yield return file;
        }
    }

    private static IEnumerable<string> EnumerateMarkup(string dir) =>
        Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(dir, "*.razor.cs", SearchOption.AllDirectories))
            .Where(path => !IsSkipped(Path.GetRelativePath(dir, path)))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool IsSkipped(string relativePath) {
        var segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Take(segments.Length - 1).Any(segment => SkippedSegments.Contains(segment, StringComparer.Ordinal));
    }
}

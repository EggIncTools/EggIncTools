using System.Text.RegularExpressions;
using EggIdentity.Styles;
using Xunit;

namespace EggIncTools.Web.Tests;

public partial class ScopedCssGuardTests {
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] MarkupProjects = ["EggIncTools.Web", "EggIncTools.Shell"];

    private static readonly HashSet<string> SharedClasses = [
        .. ComponentClasses.All.Keys.SelectMany(selector => ClassToken().Matches(selector).Select(m => m.Groups[1].Value)),
    ];

    public static TheoryData<string> ScopedFiles() {
        var data = new TheoryData<string>();
        foreach (var file in Enumerate("*.razor.css")) data.Add(Path.GetRelativePath(RepoRoot, file));
        return data;
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void ScopedFileHasNoApply(string relative) {
        Assert.DoesNotContain("@apply", Read(relative), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void EveryDeepIsAnchored(string relative) {
        foreach (var selector in Selectors(Read(relative))) {
            var at = selector.IndexOf("::deep", StringComparison.Ordinal);
            if (at < 0) continue;
            Assert.True(ClassToken().IsMatch(selector[..at]), $"{relative}: unanchored ::deep in '{selector}'");
        }
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void ScopedFileNeverRedefinesSharedClass(string relative) {
        foreach (var selector in Selectors(Read(relative))) {
            var shared = ClassToken().Matches(selector).Select(m => m.Groups[1].Value).FirstOrDefault(SharedClasses.Contains);
            Assert.True(shared is null, $"{relative}: redefines shared class .{shared} in '{selector}'");
        }
    }

    [Fact]
    public void EveryMarkupClassResolves() {
        var globalSheet = Path.Combine(RepoRoot, "EggIncTools.Web", "Styles", "app.css");
        var defined = Enumerate("*.razor.css").Append(globalSheet)
            .SelectMany(css => ClassToken().Matches(File.ReadAllText(css)).Select(m => m.Groups[1].Value))
            .Concat(SharedClasses)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Enumerate("*.razor")
            .SelectMany(razor => MarkupClasses(File.ReadAllText(razor)).Select(token => (razor, token)))
            .Where(hit => !defined.Contains(hit.token))
            .Select(hit => $"{Path.GetRelativePath(RepoRoot, hit.razor)}: {hit.token}")
            .ToList();
        Assert.Empty(missing);
    }

    private static IEnumerable<string> MarkupClasses(string razor) =>
        ClassAttribute().Matches(razor)
            .SelectMany(attr => StringLiteral().Matches(attr.Groups[1].Value).Select(m => m.Groups[1].Value)
                .Append(ExpressionOrLiteral().Replace(attr.Groups[1].Value, " ")))
            .SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(token => LiteralClass().IsMatch(token));

    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepoRoot, relative));

    private static IEnumerable<string> Selectors(string css) =>
        SelectorBlock().Matches(CommentBlock().Replace(css, "")).Select(m => m.Groups[1].Value.Trim())
            .Where(s => !s.StartsWith('@') && !KeyframeStep().IsMatch(s))
            .SelectMany(s => s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<string> Enumerate(string pattern) =>
        MarkupProjects.SelectMany(project => Directory.EnumerateFiles(Path.Combine(RepoRoot, project), pattern, SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s is "bin" or "obj" or "Tests"))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string FindRepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EggIncTools.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("EggIncTools.slnx not found above the test output.");
    }

    [GeneratedRegex(@"\.([a-zA-Z][a-zA-Z0-9-]*)")]
    private static partial Regex ClassToken();

    [GeneratedRegex(@"([^{}]+)\{")]
    private static partial Regex SelectorBlock();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentBlock();

    [GeneratedRegex(@"^(from|to|[\d.]+%)(\s*,\s*(from|to|[\d.]+%))*$")]
    private static partial Regex KeyframeStep();

    [GeneratedRegex("""(?<![\w-])class="((?:@\((?:[^()]|\([^()]*\))*\)|[^"])*)"(?=[\s/>])""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex("\"([a-z][a-z0-9 -]*)\"")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"@\((?:[^()]|\([^()]*\))*\)|@[\w.]+(?:\([^)]*\))?")]
    private static partial Regex ExpressionOrLiteral();

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex LiteralClass();
}

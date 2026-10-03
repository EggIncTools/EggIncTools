using System.Text.RegularExpressions;
using EggIdentity.Styles;
using EggIdentity.Styles.Css;
using Xunit;

namespace EggIncTools.Web.Tests;

public partial class ScopedCssGuardTests {
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] MarkupProjects = ["EggIncTools.Web", "EggIncTools.Shell"];

    private static readonly CssSheet AppSheet = CssSheet.Load(Path.Combine(RepoRoot, "EggIncTools.Web", "wwwroot", "app.css"));

    private static readonly CssSheet SharedSheet = CssSheet.Load(Path.Combine(AppContext.BaseDirectory, "shared", "shared.css"));

    private static readonly CssSheet PreflightSheet = CssSheet.Load(Path.Combine(AppContext.BaseDirectory, "shared", "preflight.css"));

    private static readonly HashSet<string> SharedClasses = [.. ClassesOf(SharedSheet)];

    private static readonly HashSet<string> InlineProperties = [
        .. Enumerate("*.razor").SelectMany(razor => StyleAttribute().Matches(File.ReadAllText(razor))
            .SelectMany(attr => InlineProperty().Matches(attr.Groups[1].Value).Select(m => m.Groups[1].Value))),
    ];

    public static TheoryData<string> ScopedFiles() {
        var data = new TheoryData<string>();
        foreach (var file in Enumerate("*.razor.css")) data.Add(Path.GetRelativePath(RepoRoot, file));
        return data;
    }

    [Fact]
    public void AppSheetDeclaresEveryContractToken() {
        var root = AppSheet.Rule(":root");
        Assert.NotNull(root);
        Assert.All(ComponentTokens.Required.Concat(ComponentTokens.Optional), token => Assert.True(root.Declares(token), token));
    }

    [Fact]
    public void AppSheetDeclaresEveryTokenTheSharedLayerReads() {
        var root = AppSheet.Rule(":root");
        Assert.NotNull(root);
        var reads = SharedSheet.ReadProperties.Where(p => p.StartsWith("--color-", StringComparison.Ordinal));
        Assert.All(reads, token => Assert.True(root.Declares(token), token));
    }

    [Fact]
    public void AppSheetMatchesRuntimeBrandTokens() {
        var root = AppSheet.Rule(":root");
        Assert.NotNull(root);
        Assert.All(BrandTokens.All, pair => Assert.Equal(pair.Value, root[pair.Key]));
    }

    [Fact]
    public void AppSheetDefinesNoClassTheSharedLayerOwns() {
        var redefined = ClassesOf(AppSheet).Where(SharedClasses.Contains).ToList();
        Assert.Empty(redefined);
    }

    [Fact]
    public void AppSheetHasNoEngineSyntax() {
        Assert.DoesNotContain(AppSheet.Statements, s => s.StartsWith('@'));
        Assert.DoesNotContain(AppSheet.Containers, c => !c.StartsWith("@media", StringComparison.Ordinal));
        Assert.DoesNotContain(AppSheet.Selectors, s => s.StartsWith('@'));
    }

    [Fact]
    public void SharedLayerDefinesNoPalette() {
        Assert.DoesNotContain(SharedSheet.DefinedProperties, p => p.StartsWith("--color-", StringComparison.Ordinal));
        Assert.DoesNotContain(PreflightSheet.DefinedProperties, p => p.StartsWith("--color-", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void ScopedFileHasNoApply(string relative) {
        Assert.DoesNotContain("@apply", Read(relative), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void EveryDeepIsAnchored(string relative) {
        foreach (var selector in SelectorsOf(Load(relative))) {
            var at = selector.IndexOf("::deep", StringComparison.Ordinal);
            if (at < 0) continue;
            Assert.True(ClassToken().IsMatch(selector[..at]), $"{relative}: unanchored ::deep in '{selector}'");
        }
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void ScopedFileNeverRedefinesSharedClass(string relative) {
        foreach (var selector in SelectorsOf(Load(relative))) {
            var shared = ClassToken().Matches(selector).Select(m => m.Groups[1].Value).FirstOrDefault(SharedClasses.Contains);
            Assert.True(shared is null, $"{relative}: redefines shared class .{shared} in '{selector}'");
        }
    }

    [Theory]
    [MemberData(nameof(ScopedFiles))]
    public void ScopedFileReadsOnlyDeclaredTokens(string relative) {
        var sheet = Load(relative);
        var root = AppSheet.Rule(":root");
        Assert.NotNull(root);
        var declared = root.Declarations.Select(d => d.Property)
            .Concat(sheet.DefinedProperties)
            .Concat(InlineProperties)
            .ToHashSet(StringComparer.Ordinal);
        var undeclared = sheet.ReadProperties.Where(p => !declared.Contains(p)).ToList();
        Assert.True(undeclared.Count == 0, $"{relative}: reads undeclared {string.Join(", ", undeclared)}");
    }

    [Fact]
    public void AppMotionPassesTheMotionGuard() {
        var violations = Enumerate("*.razor.css").Select(CssSheet.Load).Prepend(AppSheet)
            .SelectMany(sheet => MotionGuard.Check(sheet, ["egg-drift", "quad-shrink", "quad-flip"]))
            .Select(v => $"{v.Selector} {v.Property}: {v.Value} ({v.Reason})")
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void EveryMarkupClassResolves() {
        var defined = Enumerate("*.razor.css").Select(CssSheet.Load).Append(AppSheet)
            .SelectMany(ClassesOf)
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

    private static CssSheet Load(string relative) => CssSheet.Load(Path.Combine(RepoRoot, relative));

    private static IEnumerable<string> SelectorsOf(CssSheet sheet) =>
        sheet.Selectors.Where(s => !s.StartsWith('@'))
            .SelectMany(s => s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<string> ClassesOf(CssSheet sheet) =>
        SelectorsOf(sheet).SelectMany(s => ClassToken().Matches(s).Select(m => m.Groups[1].Value));

    private static IEnumerable<string> Enumerate(string pattern) =>
        MarkupProjects.SelectMany(project => Directory.EnumerateFiles(Path.Combine(RepoRoot, project), pattern, SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s is "bin" or "obj" or "dist" or "Tests"))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string FindRepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EggIncTools.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("EggIncTools.slnx not found above the test output.");
    }

    [GeneratedRegex(@"\.([a-zA-Z][a-zA-Z0-9-]*)")]
    private static partial Regex ClassToken();

    [GeneratedRegex("""(?<![\w-])class="((?:@\((?:[^()]|\([^()]*\))*\)|[^"])*)"(?=[\s/>])""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex("\"([a-z][a-z0-9 -]*)\"")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"(?<![\w-])style=""([^""]*)""")]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"(--[a-z][a-z0-9-]*)\s*:")]
    private static partial Regex InlineProperty();

    [GeneratedRegex(@"@\((?:[^()]|\([^()]*\))*\)|@[\w.]+(?:\([^)]*\))?")]
    private static partial Regex ExpressionOrLiteral();

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex LiteralClass();
}

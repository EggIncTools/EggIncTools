using EggIdentity.Deploy;
using EggIncTools.Shell;
using EggIncTools.Web.Services;
using Xunit;

namespace EggIncTools.Web.Tests;

public class ToolRegistryTests {
    [Fact]
    public void AppNamesAreTheDeployRowIdsNotTheBrandSlugs() {
        Assert.Equal("eggledger", ToolCatalog.Find("ledger")!.App);
        Assert.Equal("eggincognito", ToolCatalog.Find("incognito")!.App);
    }

    [Fact]
    public void NoRowsLeavesTheBuiltInCatalogUntouched() {
        Assert.Same(ToolCatalog.All, ToolRegistry.Apply([]));
    }

    [Fact]
    public void RowsThatMatchNoCatalogEntryAreIgnored() {
        var tools = ToolRegistry.Apply([
            App("eggidentity-agent"),
            App("eggincognito-runner"),
            App("mystery", slug: "nope"),
        ]);

        Assert.Equal(ToolCatalog.All.Select(t => t.Url), tools.Select(t => t.Url));
    }

    [Fact]
    public void PublicUrlReplacesTheCompiledUrlAndLosesItsTrailingSlash() {
        var tools = ToolRegistry.Apply([
            App("eggledger", slug: "ledger", url: "https://eggledger.example.test/"),
        ]);

        Assert.Equal("https://eggledger.example.test", Find(tools, "ledger").Url);
    }

    [Fact]
    public void ABlankBrandSlugFallsBackToMatchingTheCatalogAppName() {
        var tools = ToolRegistry.Apply([
            App("eggledger", url: "https://eggledger.davidarthurcole.me"),
        ]);

        Assert.Equal("https://eggledger.davidarthurcole.me", Find(tools, "ledger").Url);
    }

    [Fact]
    public void AnAbsentPublicUrlKeepsTheCompiledUrl() {
        var tools = ToolRegistry.Apply([App("eggledger", slug: "ledger")]);

        Assert.Equal(ToolCatalog.Find("ledger")!.Url, Find(tools, "ledger").Url);
    }

    [Fact]
    public void DisabledToolsRenderAsNotYetLive() {
        var tools = ToolRegistry.Apply([App("eggledger", slug: "ledger", enabled: false)]);

        Assert.False(Find(tools, "ledger").Live);
    }

    [Fact]
    public void ListedDoesNotGateTheHubBecauseTheCatalogDoes() {
        var tools = ToolRegistry.Apply([App("eggledger", slug: "ledger", listed: false)]);

        Assert.Contains(tools, t => t.Slug == "ledger");
        Assert.Equal(ToolCatalog.All.Count, tools.Count);
    }

    private static ToolEntry Find(IReadOnlyList<ToolEntry> tools, string slug) =>
        tools.Single(t => t.Slug == slug);

    private static DeployApp App(
        string name,
        string? slug = null,
        string? url = null,
        bool listed = true,
        bool enabled = true) =>
        new() {
            Name = name,
            BrandSlug = slug,
            PublicUrl = url,
            Listed = listed,
            Enabled = enabled,
        };
}

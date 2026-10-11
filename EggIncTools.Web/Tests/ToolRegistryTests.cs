using EggIdentity.Deploy;
using EggIncTools.Web.Services;
using Xunit;

namespace EggIncTools.Web.Tests;

public class ToolRegistryTests {
    [Fact]
    public void AppNamesAreTheDeployRowIdsNotTheBrandSlugs() {
        Assert.Equal("eggledger", ToolCards.Find("ledger")!.App);
        Assert.Equal("eggincognito", ToolCards.Find("incognito")!.App);
    }

    [Fact]
    public void NoRowsLeavesTheBuiltInCatalogUntouched() {
        Assert.Same(ToolCards.Default, ToolRegistry.Apply([]));
    }

    [Fact]
    public void RowsThatMatchNoCatalogEntryAreIgnored() {
        var tools = ToolRegistry.Apply([
            App("eggidentity-agent"),
            App("eggincognito-runner"),
            App("mystery", slug: "nope"),
        ]);

        Assert.Equal(ToolCards.Default.Select(c => c.Tool.Url), tools.Select(c => c.Tool.Url));
    }

    [Fact]
    public void PublicUrlReplacesTheCompiledUrlAndLosesItsTrailingSlash() {
        var tools = ToolRegistry.Apply([
            App("eggledger", slug: "ledger", url: "https://eggledger.example.test/"),
        ]);

        Assert.Equal("https://eggledger.example.test", Find(tools, "ledger").Tool.Url);
    }

    [Fact]
    public void ABlankBrandSlugFallsBackToMatchingTheCatalogAppName() {
        var tools = ToolRegistry.Apply([
            App("eggledger", url: "https://eggledger.example.test"),
        ]);

        Assert.Equal("https://eggledger.example.test", Find(tools, "ledger").Tool.Url);
    }

    [Fact]
    public void AnAbsentPublicUrlKeepsTheCompiledUrl() {
        var tools = ToolRegistry.Apply([App("eggledger", slug: "ledger")]);

        Assert.Equal(ToolCards.Find("ledger")!.Tool.Url, Find(tools, "ledger").Tool.Url);
    }

    [Fact]
    public void DisabledToolsRenderAsNotYetLive() {
        var tools = ToolRegistry.Apply([App("eggledger", slug: "ledger", enabled: false)]);

        Assert.False(Find(tools, "ledger").Tool.Live);
    }

    [Fact]
    public void ListedDoesNotGateTheHubBecauseTheCatalogDoes() {
        var tools = ToolRegistry.Apply([App("eggledger", slug: "ledger", listed: false)]);

        Assert.Contains(tools, c => c.Slug == "ledger");
        Assert.Equal(ToolCards.Default.Count, tools.Count);
    }

    [Fact]
    public void LiveCardsCarryPoints() {
        Assert.All(ToolCards.Default.Where(c => c.Tool.Live), card => Assert.NotEmpty(card.Points));
    }

    [Fact]
    public void EveryCardSlugHasABrand() {
        Assert.All(ToolCards.Default, card => Assert.NotNull(EggIdentity.Contract.Brands.Find(card.Slug)));
    }

    private static ToolCard Find(IReadOnlyList<ToolCard> tools, string slug) =>
        tools.Single(c => c.Slug == slug);

    private static SuiteApp App(
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

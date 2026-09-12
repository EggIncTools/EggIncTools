using EggIdentity.Contract;
using EggIdentity.Icons;
using EggIncTools.Shell;
using Xunit;

namespace EggIncTools.Web.Tests;

public class ToolCatalogTests {
    [Fact]
    public void SlugsAreUniqueAndLowercase() {
        var slugs = ToolCatalog.All.Select(t => t.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(slugs, slug => Assert.Equal(slug.ToLowerInvariant(), slug));
    }

    [Fact]
    public void EveryToolUrlIsAnHttpsEggIncToolsSubdomain() {
        Assert.All(ToolCatalog.All, tool => {
            Assert.True(Uri.TryCreate(tool.Url, UriKind.Absolute, out var parsed), tool.Url);
            Assert.Equal(Uri.UriSchemeHttps, parsed!.Scheme);
            Assert.EndsWith(".egginc.tools", parsed.Host, StringComparison.Ordinal);
            Assert.Equal("/", parsed.AbsolutePath);
        });
    }

    [Fact]
    public void EveryIconUrlPointsAtThePackagedBrandAsset() {
        Assert.All(ToolCatalog.All, tool =>
            Assert.Equal($"{Brands.AssetBase}/{tool.Slug}/icon-512.png", tool.IconUrl));
    }

    [Fact]
    public void NameAndAccentComeFromTheBrandTable() {
        Assert.All(ToolCatalog.All, tool => {
            var brand = Brands.Find(tool.Slug)!;
            Assert.Equal(brand.DisplayName, tool.Name);
            Assert.Equal(brand.Accent, tool.Accent);
        });
    }

    [Fact]
    public void EveryAccentIsASixDigitHex() {
        Assert.All(ToolCatalog.All, tool => {
            Assert.Matches("^#[0-9a-f]{6}$", tool.Accent);
        });
    }

    [Fact]
    public void FindIsCaseInsensitiveAndNullSafe() {
        Assert.Equal("ledger", ToolCatalog.Find("LEDGER")?.Slug);
        Assert.Null(ToolCatalog.Find("nope"));
        Assert.Null(ToolCatalog.Find(null));
    }

    [Fact]
    public void SignOutIsOnTheIdentityOrigin() {
        Assert.Equal("https://egginc.tools", ToolCatalog.HubUrl);
        Assert.Equal($"{ToolCatalog.IdentityOrigin}/auth/logout", ToolCatalog.SignOutUrl);
    }

    [Fact]
    public void EveryPackSourcedProviderIconExists() {
        var packSourced = ToolCatalog.Providers.Where(p => p is not ("google" or "microsoft"));
        Assert.All(packSourced, provider =>
            Assert.True(IconPack.TryGet(ToolCatalog.ProviderIcon(provider), out _), provider));
    }

    [Fact]
    public void ProviderSignInUrlsTargetTheIdentityHostAndEscapeTheReturn() {
        Assert.All(ToolCatalog.Providers, provider => {
            var url = ToolCatalog.ProviderSignInUrl(provider, "https://egginc.tools/?a=b");
            Assert.StartsWith($"{ToolCatalog.IdentityOrigin}/auth/go/{provider}?returnUrl=", url, StringComparison.Ordinal);
            Assert.EndsWith("&mode=redirect", url, StringComparison.Ordinal);
            Assert.DoesNotContain("?a=b", url, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("https://egginc.tools")]
    [InlineData("https://egginc.tools/some/path")]
    [InlineData("https://eggledger.egginc.tools/")]
    [InlineData("http://localhost:8090/")]
    public void AllowedReturnsSurviveTheRoundTrip(string returnUrl) {
        Assert.True(ToolCatalog.IsAllowedReturn(returnUrl));
        Assert.Contains(Uri.EscapeDataString(returnUrl), ToolCatalog.SignOutUrlFor(returnUrl), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://egginc.tools.evil.example")]
    [InlineData("https://evilegginc.tools")]
    [InlineData("https://egginc.tools@evil.example")]
    [InlineData("http://egginc.tools")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void ForeignReturnsFallBackToTheHub(string returnUrl) {
        Assert.False(ToolCatalog.IsAllowedReturn(returnUrl));
        Assert.Equal(
            $"{ToolCatalog.SignOutUrl}?returnUrl={Uri.EscapeDataString(ToolCatalog.HubUrl)}",
            ToolCatalog.SignOutUrlFor(returnUrl));
        Assert.All(ToolCatalog.Providers, provider =>
            Assert.Contains(
                $"returnUrl={Uri.EscapeDataString(ToolCatalog.HubUrl)}",
                ToolCatalog.ProviderSignInUrl(provider, returnUrl),
                StringComparison.Ordinal));
    }

    [Fact]
    public void LiveToolsCarryPoints() {
        Assert.All(ToolCatalog.All.Where(t => t.Live), tool =>
            Assert.NotEmpty(tool.Points));
    }
}

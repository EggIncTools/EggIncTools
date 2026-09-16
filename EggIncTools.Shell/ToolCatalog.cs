using EggIdentity.Contract;

namespace EggIncTools.Shell;

public sealed record ToolEntry(
    string Slug,
    string Url,
    bool Live = true) {
    private readonly BrandInfo _brand = Brands.Find(Slug)
        ?? throw new InvalidOperationException($"No brand for {Slug}");

    public string Name => _brand.DisplayName;

    public string Accent => _brand.Accent;

    public string IconUrl => Brands.IconPath(Slug, 512);

    public string MarkStyle => ToolCatalog.MarkStyleFor(Slug);

    public string App { get; init; } = Slug;

    public IReadOnlyList<string> Points { get; init; } = [];
}

public static class ToolCatalog {
    public const string HubHost = "egginc.tools";
    public const string HubUrl = $"https://{HubHost}";
    public const string IdentityOriginEnv = "IDENTITY_WIDGET_URL";
    public const string DefaultIdentityOrigin = "https://eggidentity.egginc.tools";

    public static IReadOnlyList<string> ReturnHosts { get; } = [HubHost];

    public static Action<string>? OnReturnRejected { get; set; }

    public static string IdentityOrigin { get; } = ResolveIdentityOrigin();

    public static string SignOutUrl => $"{IdentityOrigin}/auth/logout";

    private static string ResolveIdentityOrigin() {
        var raw = Environment.GetEnvironmentVariable(IdentityOriginEnv)?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(raw)) return DefaultIdentityOrigin;

        return Uri.TryCreate(raw, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.IsLoopback)
            ? raw
            : DefaultIdentityOrigin;
    }
    public const string DiscordUrl = $"https://discord.{HubHost}";
    public const string GitHubUrl = "https://github.com/EggIncTools";
    public const string AuthorUrl = "https://github.com/DavidArthurCole";
    public const string AuthorName = "Daveed";
    public const string DiscordName = "DVD";

    public const string DiscordIconUrl =
        "https://cdn.discordapp.com/icons/1497680181274345585/474ca745b56bd69bb1518768f1edbd38.webp";

    private const double BaseGutter = 0.078125;

    private static readonly Dictionary<string, double> Gutters = new(StringComparer.OrdinalIgnoreCase) {
        ["ledger"] = 0.15625,
        ["incognito"] = 0.21875,
        ["abacus"] = 0.09375,
        ["identity"] = 0.0546875,
        ["tools"] = BaseGutter,
    };

    public static string MarkStyleFor(string slug) {
        if (!Gutters.TryGetValue(slug, out var gutter) || gutter <= BaseGutter) return "";

        var scale = (1 - 2 * BaseGutter) / (1 - 2 * gutter);
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"scale: {scale:0.###}");
    }

    public static string HubIconUrl(int size) => Brands.IconPath("tools", size);

    public static string HubFaviconUrl => Brands.FaviconPath("tools");

    public static IReadOnlyList<ToolEntry> All { get; } = [
        new("ledger", "https://eggledger.egginc.tools") {
            App = "eggledger",
            Points = [
                "View and filter all ships you've ever launched",
                "Write reports to parse your data",
                "Export ship drops to XLSX/CSV",
            ],
        },
        new("incognito", "https://eggincognito.egginc.tools") {
            App = "eggincognito",
            Points = [
                "Live API data for all things Egg, Inc.",
                "Realtime notifications for new game releases",
                "Historical feed of Egg, Inc. protobuf versions",
            ],
        },
        new("abacus", "https://eggabacus.egginc.tools", Live: false) { App = "eggabacus" },
    ];

    public static IReadOnlyList<string> Providers { get; } = ["discord", "google", "microsoft", "github"];

    public static string ProviderIcon(string provider) =>
        provider switch {
            "microsoft" => "layout-grid",
            _ => $"brand-{provider}",
        };

    public static string ProviderSignInUrl(string provider, string returnUrl) =>
        $"{IdentityOrigin}/auth/go/{provider}?returnUrl={Uri.EscapeDataString(SafeReturn(returnUrl))}&mode=redirect";

    public static string SignOutUrlFor(string returnUrl) =>
        $"{SignOutUrl}?returnUrl={Uri.EscapeDataString(SafeReturn(returnUrl))}";

    public static bool IsAllowedReturn(string? returnUrl) =>
        Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps || parsed.IsLoopback)
        && (parsed.IsLoopback || ReturnHosts.Any(host => IsHostOrSubdomain(parsed.Host, host)));

    private static bool IsHostOrSubdomain(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase)
        || candidate.EndsWith($".{parent}", StringComparison.OrdinalIgnoreCase);

    private static string SafeReturn(string returnUrl) {
        if (IsAllowedReturn(returnUrl)) return returnUrl;

        OnReturnRejected?.Invoke(
            Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed) ? parsed.Host : "(not an absolute url)");
        return HubUrl;
    }

    public static ToolEntry? Find(string? slug) =>
        slug is null ? null : All.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
}

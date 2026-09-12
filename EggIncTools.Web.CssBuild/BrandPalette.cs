using EggIdentity.Fallback;
using EggIdentity.Styles;
using EggIdentity.Styles.Theming;

namespace EggIncTools.Web.CssBuild;

public static class BrandPalette {
    private const string ColorPrefix = "--color-";

    public static IReadOnlyList<(string Name, string Value)> ComponentColors { get; } = [
        .. ComponentTokens.Required.Concat(ComponentTokens.Optional)
            .Select(cssVar => (Name: cssVar[ColorPrefix.Length..], Value: FallbackDefaults.Tokens[cssVar]))
    ];

    public static IReadOnlyList<(string Name, string Value)> AppColors { get; } = [
        ("warn", "#f0b232"),
        ("down", "#8992a4"),
    ];

    public static IReadOnlyList<string> StatusTokens { get; } = ["accent", "ok", "err"];

    public static IReadOnlyList<string> ContrastBaseTokens { get; } = ["bg", "panel0", "panel", "panel2", "fg", "muted", "border"];

    public static IReadOnlyDictionary<string, string> Tokens { get; } =
        ComponentColors.ToDictionary(c => ColorPrefix + c.Name, c => c.Value);

    public static ThemeTokenRegistry BuildRegistry() {
        var registry = new ThemeTokenRegistry();
        foreach (var (name, _) in AppColors) {
            registry.Register(name);
        }
        return registry;
    }
}

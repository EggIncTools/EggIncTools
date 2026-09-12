using EggIdentity.Fallback;

namespace EggIncTools.Web;

public static class BrandTokens {
    public static IReadOnlyDictionary<string, string> All { get; } = FallbackDefaults.Tokens;
}

using EggIdentity.Deploy;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIncTools.Web.Services;

public sealed class ToolRegistry(SettingsCache cache, ILogger<ToolRegistry> log) {
    public async Task<IReadOnlyList<ToolCard>> ToolsAsync(CancellationToken ct = default) =>
        Apply(await RowsAsync(ct));

    public static IReadOnlyList<ToolCard> Apply(IReadOnlyList<SuiteApp> registered) {
        if (registered.Count == 0) return ToolCards.Default;

        var bySlug = new Dictionary<string, SuiteApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in registered) {
            if (ResolveSlug(app) is { } slug) bySlug[slug] = app;
        }

        return [.. ToolCards.Default
            .Select(card => bySlug.TryGetValue(card.Slug, out var app) ? Merge(card, app) : card)];
    }

    public async Task<IReadOnlyList<SuiteApp>> RowsAsync(CancellationToken ct = default) {
        try {
            var snapshot = await cache.GetAsync(ct);
            return [.. snapshot.Rows(SuiteApps.Key).Select(Project).OfType<SuiteApp>()];
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "could not read {Collection}; falling back to the built-in catalog", SuiteApps.Key);
            return [];
        }
    }

    private SuiteApp? Project(CollectionRow row) {
        try {
            var app = CollectionBinder.Bind<SuiteApp>(row.Values);
            return string.IsNullOrWhiteSpace(app.Name) ? app with { Name = row.Id } : app;
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "skipping malformed {Collection} row {Row}", SuiteApps.Key, row.Id);
            return null;
        }
    }

    private static string? ResolveSlug(SuiteApp app) {
        if (!string.IsNullOrWhiteSpace(app.BrandSlug)) return ToolCards.Find(app.BrandSlug)?.Slug;

        return ToolCards.Default.FirstOrDefault(c =>
            string.Equals(c.App, app.Name, StringComparison.OrdinalIgnoreCase))?.Slug;
    }

    private static ToolCard Merge(ToolCard card, SuiteApp app) {
        var url = app.PublicUrl?.TrimEnd('/');
        var entry = card.Tool with {
            Url = string.IsNullOrWhiteSpace(url) ? card.Tool.Url : url,
            Live = card.Tool.Live && app.Enabled,
        };
        return card with {
            Tool = entry,
            App = string.IsNullOrWhiteSpace(app.Name) ? card.App : app.Name,
        };
    }
}

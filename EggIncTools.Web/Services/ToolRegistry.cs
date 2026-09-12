using EggIdentity.Deploy;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using EggIncTools.Shell;

namespace EggIncTools.Web.Services;

public sealed class ToolRegistry(SettingsCache cache, ILogger<ToolRegistry> log) {
    public async Task<IReadOnlyList<ToolEntry>> ToolsAsync(CancellationToken ct = default) =>
        Apply(await RowsAsync(ct));

    public static IReadOnlyList<ToolEntry> Apply(IReadOnlyList<DeployApp> registered) {
        if (registered.Count == 0) return ToolCatalog.All;

        var bySlug = registered
            .Select(app => (Slug: ResolveSlug(app), App: app))
            .Where(pair => pair.Slug is not null)
            .ToDictionary(pair => pair.Slug!, pair => pair.App, StringComparer.OrdinalIgnoreCase);

        return [.. ToolCatalog.All
            .Select(tool => bySlug.TryGetValue(tool.Slug, out var app) ? Merge(tool, app) : tool)];
    }

    public async Task<IReadOnlyList<DeployApp>> RowsAsync(CancellationToken ct = default) {
        try {
            var snapshot = await cache.GetAsync(ct);
            return [.. snapshot.Rows(DeployApps.Key).Select(Project).OfType<DeployApp>()];
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "could not read {Collection}; falling back to the built-in catalog", DeployApps.Key);
            return [];
        }
    }

    private DeployApp? Project(CollectionRow row) {
        try {
            var app = CollectionBinder.Bind<DeployApp>(row.Values);
            return string.IsNullOrWhiteSpace(app.Name) ? app with { Name = row.Id } : app;
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "skipping malformed {Collection} row {Row}", DeployApps.Key, row.Id);
            return null;
        }
    }

    private static string? ResolveSlug(DeployApp app) {
        if (!string.IsNullOrWhiteSpace(app.BrandSlug)) return ToolCatalog.Find(app.BrandSlug)?.Slug;

        return ToolCatalog.All.FirstOrDefault(t =>
            string.Equals(t.App, app.Name, StringComparison.OrdinalIgnoreCase))?.Slug;
    }

    private static ToolEntry Merge(ToolEntry tool, DeployApp app) {
        var url = app.PublicUrl?.TrimEnd('/');
        return tool with {
            Url = string.IsNullOrWhiteSpace(url) ? tool.Url : url,
            Live = tool.Live && app.Enabled,
            App = string.IsNullOrWhiteSpace(app.Name) ? tool.App : app.Name,
        };
    }
}

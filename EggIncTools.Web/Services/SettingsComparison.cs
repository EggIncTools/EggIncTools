using EggIdentity.Contract;
using EggIdentity.Settings.Api;

namespace EggIncTools.Web.Services;

public sealed record CompareCell(string App, string? Display, string? Source, bool Present);

public sealed record CompareRow(
    string Key, string Label, string Category, bool Secret, IReadOnlyList<CompareCell> Cells) {
    public IEnumerable<CompareCell> Set => Cells.Where(c => c.Present);

    public int SetCount => Set.Count();

    public bool Divergent =>
        !Secret && Set.Select(c => c.Display ?? "").Distinct(StringComparer.Ordinal).Count() > 1;
}

public sealed record CompareApp(AdminTargetStatus Status, string? Error) {
    public string App => Status.App;

    public bool Ok => Status.Administrable && Error is null;

    public string? Unavailable => Status.Unavailable ?? Error;
}

public sealed record ComparisonReport(IReadOnlyList<CompareApp> Apps, IReadOnlyList<CompareRow> Rows) {
    public static ComparisonReport Empty { get; } = new([], []);
}

internal sealed record FetchedSettings(CompareApp App, AdminSettingsResponse? Settings);

public sealed class SettingsComparison(
    AdminTargetsService targets, AdminApiClient client, ILogger<SettingsComparison> log) {
    public async Task<ComparisonReport> BuildAsync(bool refresh = false, CancellationToken ct = default) {
        var statuses = await targets.StatusesAsync(refresh, ct);
        if (statuses.Count == 0) return ComparisonReport.Empty;

        var fetched = await Task.WhenAll(statuses.Select(status => FetchAsync(status, ct)));
        return new ComparisonReport([.. fetched.Select(f => f.App)], Pivot(fetched));
    }

    private async Task<FetchedSettings> FetchAsync(AdminTargetStatus status, CancellationToken ct) {
        if (status.Target is not { } target) return new(new CompareApp(status, null), null);

        try {
            return new(new CompareApp(status, null), await client.GetSettingsAsync(target, ct));
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "could not read settings from {App}", status.App);
            return new(new CompareApp(status, Describe(exc)), null);
        }
    }

    private static string Describe(Exception exc) =>
        exc is HttpRequestException or TimeoutException or TaskCanceledException
            ? exc.Message
            : "this app did not answer with a settings document";

    private static IReadOnlyList<CompareRow> Pivot(IReadOnlyList<FetchedSettings> fetched) {
        var byApp = fetched.ToDictionary(
            f => f.App.App,
            f => f.Settings?.Settings.ToDictionary(s => s.Key, StringComparer.Ordinal)
                ?? [],
            StringComparer.Ordinal);

        var keys = byApp.Values
            .SelectMany(settings => settings.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return [.. keys.Select(key => Row(key, fetched, byApp))
            .OrderBy(row => row.Category, StringComparer.Ordinal)
            .ThenBy(row => row.Key, StringComparer.Ordinal)];
    }

    private static CompareRow Row(
        string key,
        IReadOnlyList<FetchedSettings> fetched,
        IReadOnlyDictionary<string, Dictionary<string, AdminSettingWire>> byApp) {
        var any = byApp.Values.Select(s => s.GetValueOrDefault(key)).OfType<AdminSettingWire>().First();

        var cells = fetched.Select(f => {
            var wire = byApp[f.App.App].GetValueOrDefault(key);
            return new CompareCell(
                f.App.App,
                wire?.Display,
                wire?.Source,
                wire is { Display.Length: > 0 });
        });

        return new CompareRow(key, any.Label, any.Category, any.Secret, [.. cells]);
    }
}

using EggIdentity.Deploy;
using EggIdentity.Deploy.AdminUi;
using EggIdentity.Settings;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;

namespace EggIncTools.Web.Services;

public sealed class AdminTargetsService(SettingsCache cache, ILogger<AdminTargetsService> log) {
    public async Task<IReadOnlyList<AdminTargetStatus>> StatusesAsync(
        bool refresh = false, CancellationToken ct = default) {
        try {
            var snapshot = refresh ? await cache.RefreshAsync(ct) : await cache.GetAsync(ct);
            return [.. snapshot.Rows(SuiteApps.Key)
                .Select(Project)
                .OfType<SuiteApp>()
                .Where(app => !string.IsNullOrWhiteSpace(app.AdminBaseUrl))
                .Select(app => SuiteAppTargets.Describe(app, Environment.GetEnvironmentVariable))
                .GroupBy(status => status.App, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(status => status.App, StringComparer.OrdinalIgnoreCase)];
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "could not read {Collection}; the comparison will show no remote apps", SuiteApps.Key);
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
}

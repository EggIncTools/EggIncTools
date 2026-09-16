using EggIdentity.Settings;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;

namespace EggIncTools.Web.Services;

public sealed class AdminTargetsService(SettingsCache cache, ILogger<AdminTargetsService> log) {
    public async Task<IReadOnlyList<AdminTargetStatus>> StatusesAsync(
        bool refresh = false, CancellationToken ct = default) {
        try {
            var snapshot = refresh ? await cache.RefreshAsync(ct) : await cache.GetAsync(ct);
            return [.. snapshot.Rows(AdminTargets.Key)
                .Select(Project)
                .OfType<AdminTargetRow>()
                .Select(row => AdminTargets.Describe(row, Environment.GetEnvironmentVariable))
                .GroupBy(status => status.App, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(status => status.App, StringComparer.OrdinalIgnoreCase)];
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "could not read {Collection}; the admin pane will show no remote apps", AdminTargets.Key);
            return [];
        }
    }

    private AdminTargetRow? Project(CollectionRow row) {
        try {
            var target = CollectionBinder.Bind<AdminTargetRow>(row.Values);
            return string.IsNullOrWhiteSpace(target.Name) ? target with { Name = row.Id } : target;
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "skipping malformed {Collection} row {Row}", AdminTargets.Key, row.Id);
            return null;
        }
    }
}

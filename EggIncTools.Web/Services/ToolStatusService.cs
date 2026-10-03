using System.Collections.Frozen;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Hosting;
using EggIdentity.Resilience;

namespace EggIncTools.Web.Services;

public enum ToolState { Unknown, Up, Deploying, Failed }

public sealed record ToolHealth(
    string Slug,
    string? RunningVersion,
    ToolState State,
    DateTimeOffset CheckedAt) {
    public string Label =>
        State switch {
            ToolState.Up => "Up",
            ToolState.Deploying => "Deploying",
            ToolState.Failed => "Deploy failed",
            _ => "Unknown",
        };
}

public sealed class ToolStatusService(
    ToolRegistry registry,
    IServiceScopeFactory scopes,
    ILogger<ToolStatusService> log,
    TimeProvider time,
    FleetClient? fleet = null) : PeriodicScopedService(scopes, time, log) {
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger _log = log;
    private readonly TimeProvider _time = time;

    private volatile IReadOnlyDictionary<string, ToolHealth> _snapshot =
        new Dictionary<string, ToolHealth>(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public IReadOnlyDictionary<string, ToolHealth> Snapshot => _snapshot;

    protected override TimeSpan Interval => TimeSpan.FromSeconds(60);

    public ToolHealth? For(string slug) => _snapshot.GetValueOrDefault(slug);

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken ct) => PollOnceAsync(ct);

    public async Task PollOnceAsync(CancellationToken ct) {
        var versions = await ReadVersionsAsync(ct);
        var tools = await registry.ToolsAsync(ct);
        var now = _time.GetUtcNow();

        _snapshot = tools
            .Where(tool => tool.Live)
            .Select(tool => Describe(tool.Slug, versions.GetValueOrDefault(tool.App), now))
            .ToDictionary(h => h.Slug, h => h, StringComparer.OrdinalIgnoreCase);

        foreach (var handler in Changed?.GetInvocationList() ?? []) {
            try {
                ((Action)handler)();
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Changed -= (Action)handler;
                _log.LogDebug(exc, "dropped a stale tool status subscriber");
            }
        }
    }

    private static ToolHealth Describe(string slug, DeployStatus? status, DateTimeOffset now) {
        if (status is null) return new ToolHealth(slug, null, ToolState.Unknown, now);

        var state = status switch {
            { Busy: true } => ToolState.Deploying,
            { LastEvent.Phase: DeployPhase.Failed } => ToolState.Failed,
            { RunningVersion.Length: > 0 } => ToolState.Up,
            _ => ToolState.Unknown,
        };

        return new ToolHealth(slug, status.RunningVersion, state, now);
    }

    private async Task<IReadOnlyDictionary<string, DeployStatus>> ReadVersionsAsync(CancellationToken ct) {
        if (fleet is null) return FrozenDictionary<string, DeployStatus>.Empty;
        try {
            var all = await Deadline.RunAsync("fleet status", fleet.GetAllStatusAsync, ProbeTimeout, _time, ct);
            return all.ToDictionary(s => s.App, s => s, StringComparer.OrdinalIgnoreCase);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            _log.LogDebug(exc, "fleet status read failed; tool cards show no version");
            return FrozenDictionary<string, DeployStatus>.Empty;
        }
    }
}

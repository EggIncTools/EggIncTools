using System.Collections.Frozen;
using EggIdentity.Contract;
using EggIdentity.Deploy;
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
    ILogger<ToolStatusService> log,
    TimeProvider time,
    FleetClient? fleet = null) : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private volatile IReadOnlyDictionary<string, ToolHealth> _snapshot =
        new Dictionary<string, ToolHealth>(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public IReadOnlyDictionary<string, ToolHealth> Snapshot => _snapshot;

    public ToolHealth? For(string slug) => _snapshot.GetValueOrDefault(slug);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await PollOnceAsync(stoppingToken);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                log.LogError(exc, "tool status poll failed");
            }
            try {
                await Task.Delay(Interval, time, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken ct) {
        var versions = await ReadVersionsAsync(ct);
        var tools = await registry.ToolsAsync(ct);
        var now = time.GetUtcNow();

        _snapshot = tools
            .Where(tool => tool.Live)
            .Select(tool => Describe(tool.Slug, versions.GetValueOrDefault(tool.App), now))
            .ToDictionary(h => h.Slug, h => h, StringComparer.OrdinalIgnoreCase);

        foreach (var handler in Changed?.GetInvocationList() ?? []) {
            try {
                ((Action)handler)();
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Changed -= (Action)handler;
                log.LogDebug(exc, "dropped a stale tool status subscriber");
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
            var all = await Deadline.RunAsync("fleet status", fleet.GetAllStatusAsync, ProbeTimeout, time, ct);
            return all.ToDictionary(s => s.App, s => s, StringComparer.OrdinalIgnoreCase);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogDebug(exc, "fleet status read failed; tool cards show no version");
            return FrozenDictionary<string, DeployStatus>.Empty;
        }
    }
}

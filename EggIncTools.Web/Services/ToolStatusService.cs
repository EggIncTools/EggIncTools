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
    IServiceProvider services,
    ToolRegistry registry,
    TimeProvider? time = null) : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _clock = time ?? TimeProvider.System;
    private volatile IReadOnlyDictionary<string, ToolHealth> _snapshot =
        new Dictionary<string, ToolHealth>(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public IReadOnlyDictionary<string, ToolHealth> Snapshot => _snapshot;

    public ToolHealth? For(string slug) => _snapshot.GetValueOrDefault(slug);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var log = services.GetRequiredService<ILogger<ToolStatusService>>();
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await PollOnceAsync(stoppingToken);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                log.LogError(exc, "tool status poll failed");
            }
            try {
                await Task.Delay(Interval, _clock, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken ct) {
        var versions = await ReadVersionsAsync(ct);
        var tools = await registry.ToolsAsync(ct);
        var now = _clock.GetUtcNow();

        _snapshot = tools
            .Where(tool => tool.Live)
            .Select(tool => Describe(tool.Slug, versions.GetValueOrDefault(tool.App), now))
            .ToDictionary(h => h.Slug, h => h, StringComparer.OrdinalIgnoreCase);

        foreach (var handler in Changed?.GetInvocationList() ?? []) {
            try {
                ((Action)handler)();
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Changed -= (Action)handler;
                services.GetRequiredService<ILogger<ToolStatusService>>()
                    .LogDebug(exc, "dropped a stale tool status subscriber");
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
        var agent = services.GetService<AgentClient>();
        if (agent is null) return new Dictionary<string, DeployStatus>(StringComparer.OrdinalIgnoreCase);
        try {
            var all = await Deadline.RunAsync("agent status", agent.GetAllStatusAsync, ProbeTimeout, _clock, ct);
            return all.ToDictionary(s => s.App, s => s, StringComparer.OrdinalIgnoreCase);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            return new Dictionary<string, DeployStatus>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

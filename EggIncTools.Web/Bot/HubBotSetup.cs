using System.Reflection;
using EggIdentity.Bot;
using EggIdentity.Client;
using EggIdentity.Contract;
using EggIncTools.Web.Services;

namespace EggIncTools.Web.Bot;

public static class HubBotSetup {
    public const string AppName = "egginctools";
    public const string BotName = "EggIncTools";
    public const string RepoUrl = "https://github.com/EggIncTools/EggIncTools";
    public const string MigrationsDir = "Bot/Migrations";

    public static void AddHubBot(this IServiceCollection services, HostConfig config) {
        if (!config.BotEnabled) return;

        var build = Build();
        var startedAt = TimeProvider.System.GetUtcNow();
        services.AddSingleton(sp => new HubBot(
            ConfigFor(config.BotConnString!, build, startedAt, sp.GetService<ToolStatusService>()),
            ct => sp.GetRequiredService<IdentityApiClient>().GetDiscordRegistrationAsync(AppName, ct),
            sp.GetRequiredService<ILogger<HubBot>>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddHostedService(sp => sp.GetRequiredService<HubBot>());
    }

    public static BotConfig ConfigFor(
        string connString, VerifyInfo build, DateTimeOffset startedAt, ToolStatusService? status) =>
        new() {
            Name = BotName,
            RepoUrl = RepoUrl,
            Build = build,
            PostgresConnectionString = connString,
            MigrationsDir = MigrationsDir,
            DashboardProvider = _ => Task.FromResult(Dashboard(build, startedAt, status)),
        };

    public static DashboardSnapshot Dashboard(VerifyInfo build, DateTimeOffset startedAt, ToolStatusService? status) =>
        new() {
            AppName = BotName,
            Version = build.Version,
            BuildHash = build.Sha256,
            DeployStatus = "online",
            UptimeSince = startedAt,
            RepoUrl = RepoUrl,
            ExtraFields = status?.Snapshot.Values
                .OrderBy(h => h.Slug, StringComparer.Ordinal)
                .ToDictionary(h => h.Slug, h => h.RunningVersion is { Length: > 0 } v ? $"{h.Label} ({v})" : h.Label)
                ?? [],
        };

    private static VerifyInfo Build() =>
        new() {
            Name = BotName,
            Sha256 = Environment.GetEnvironmentVariable("GIT_SHA") ?? "",
            Version = typeof(HubBotSetup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "dev",
        };
}

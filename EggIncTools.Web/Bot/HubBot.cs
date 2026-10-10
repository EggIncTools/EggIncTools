using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIdentity.Resilience;

namespace EggIncTools.Web.Bot;

public sealed class HubBot(
    BotConfig baseConfig,
    Func<CancellationToken, Task<DiscordRegistrationResponse?>> registration,
    ILogger<HubBot> log,
    TimeProvider time,
    Func<BotConfig, Task<EggIdentityBot?>>? start = null) : BackgroundService {
    private static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(10);

    private readonly Func<BotConfig, Task<EggIdentityBot?>> _start = start ?? (cfg => EggIdentityBot.StartAsync(cfg));

    public EggIdentityBot? Bot { get; private set; }

    public BotConfig Config { get; private set; } = baseConfig;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        DiscordRegistrationResponse? reg;
        try {
            reg = await Deadline.RunAsync("discord registration", registration, RegistrationTimeout, time, stoppingToken);
        } catch (Exception exc) when (exc is HttpRequestException or TimeoutException or System.Text.Json.JsonException) {
            log.LogWarning(exc, "bot: could not read the Discord registration from the identity API; the bot stays off until the next restart");
            return;
        }

        if (reg is null) {
            log.LogInformation("bot: no Discord registration for {App} (row not enabled, not set to own, or missing token or guild id); the bot stays off", Config.Name);
            return;
        }

        Config = Config.WithRegistration(reg);
        try {
            Bot = await _start(Config);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "bot: failed to start; check the token and that the Server Members intent is enabled for the application. The hub keeps running without it");
            return;
        }

        if (Bot is null) return;
        Bot.Client.Ready += SelfReportAsync;
    }

    private async Task SelfReportAsync() {
        if (Bot is not { } bot) return;
        try {
            await bot.TrackDeployAsync(Config.Name, Config.Build.Sha256, Config.Build.Version);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            log.LogWarning(exc, "bot: deploy self-report failed");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        await base.StopAsync(cancellationToken);
        if (Bot is { } bot) {
            bot.Client.Ready -= SelfReportAsync;
            await bot.DisposeAsync();
        }
    }
}

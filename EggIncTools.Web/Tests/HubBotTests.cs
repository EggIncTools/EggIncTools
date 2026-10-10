using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIncTools.Web.Bot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EggIncTools.Web.Tests;

public sealed class HubBotTests {
    private static readonly BotConfig Base = HubBotSetup.ConfigFor(
        "Host=bot", new VerifyInfo { Name = HubBotSetup.BotName, Version = "1.2.3" }, DateTimeOffset.UnixEpoch, null);

    [Fact]
    public async Task NoRegistrationLeavesTheBotOffWithoutStartingIt() {
        var started = 0;
        var bot = Build(_ => Task.FromResult<DiscordRegistrationResponse?>(null), _ => {
            started++;
            return Task.FromResult<EggIdentityBot?>(null);
        });

        await RunAsync(bot);

        Assert.Equal(0, started);
        Assert.Null(bot.Bot);
        Assert.Same(Base, bot.Config);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(System.Text.Json.JsonException))]
    public async Task AFailedRegistrationReadIsSwallowedAndTheBotStaysOff(Type thrown) {
        var started = 0;
        var bot = Build(_ => throw (Exception)Activator.CreateInstance(thrown)!, _ => {
            started++;
            return Task.FromResult<EggIdentityBot?>(null);
        });

        await RunAsync(bot);

        Assert.Equal(0, started);
        Assert.Null(bot.Bot);
    }

    [Fact]
    public async Task ARegistrationIsOverlaidBeforeTheBotStarts() {
        BotConfig? seen = null;
        var bot = Build(
            _ => Task.FromResult<DiscordRegistrationResponse?>(
                new DiscordRegistrationResponse { Token = "tok", GuildId = "42", AppId = "7" }),
            cfg => {
                seen = cfg;
                return Task.FromResult<EggIdentityBot?>(null);
            });

        await RunAsync(bot);

        Assert.NotNull(seen);
        Assert.Equal("tok", seen.Token);
        Assert.Equal("42", seen.GuildId);
        Assert.Equal("7", seen.AppId);
        Assert.Equal("Host=bot", seen.PostgresConnectionString);
        Assert.Equal(HubBotSetup.MigrationsDir, seen.MigrationsDir);
    }

    [Fact]
    public async Task AStartFailureIsSwallowedSoTheHubKeepsRunning() {
        var bot = Build(
            _ => Task.FromResult<DiscordRegistrationResponse?>(new DiscordRegistrationResponse { Token = "tok", GuildId = "42" }),
            _ => throw new InvalidOperationException("gateway rejected"));

        await RunAsync(bot);

        Assert.Null(bot.Bot);
    }

    [Fact]
    public void TheMigrationsShipNextToTheAssemblyUnderTheConfiguredDir() {
        var dir = Path.Combine(AppContext.BaseDirectory, HubBotSetup.MigrationsDir);

        var files = Directory.GetFiles(dir, "*.up.sql").Select(Path.GetFileName).ToList();

        Assert.Contains("2_bot_channel_config.up.sql", files);
        Assert.Contains("7_deploy_state.up.sql", files);
    }

    [Fact]
    public void TheBotIsOnlyRegisteredWithItsOwnDatabaseAndTheIdentityApi() {
        Assert.False(Registered(Config(bot: null, apiUrl: "http://id", apiSecret: "s")));
        Assert.False(Registered(Config(bot: "Host=bot", apiUrl: null, apiSecret: "s")));
        Assert.True(Registered(Config(bot: "Host=bot", apiUrl: "http://id", apiSecret: "s")));
    }

    [Fact]
    public void TheDashboardListsEachToolWithoutAStatusSourceToo() {
        var snap = HubBotSetup.Dashboard(new VerifyInfo { Version = "1.2.3", Sha256 = "abc" }, DateTimeOffset.UnixEpoch, null);

        Assert.Equal(HubBotSetup.BotName, snap.AppName);
        Assert.Equal("1.2.3", snap.Version);
        Assert.Empty(snap.ExtraFields);
    }

    private static HubBot Build(
        Func<CancellationToken, Task<DiscordRegistrationResponse?>> registration,
        Func<BotConfig, Task<EggIdentityBot?>> start) =>
        new(Base, registration, NullLogger<HubBot>.Instance, TimeProvider.System, start);

    private static async Task RunAsync(IHostedService bot) {
        await bot.StartAsync(CancellationToken.None);
        await ((BackgroundService)bot).ExecuteTask!;
        await bot.StopAsync(CancellationToken.None);
    }

    private static HostConfig Config(string? bot, string? apiUrl, string? apiSecret) =>
        new() {
            ConnString = "Host=identity",
            BotConnString = bot,
            HubOnly = false,
            Port = 8090,
            IdentityApiUrl = apiUrl,
            IdentityApiSecret = apiSecret,
        };

    private static bool Registered(HostConfig config) {
        var services = new ServiceCollection();
        services.AddHubBot(config);
        return services.Any(d => d.ServiceType == typeof(HubBot));
    }
}

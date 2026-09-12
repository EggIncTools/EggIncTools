using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using EggIncTools.Shell;
using EggIncTools.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace EggIncTools.Web.Tests;

public class ToolStatusServiceTests {
    private const string Unreachable = "Host=/nonexistent-socket-dir;Database=none;Username=none;Timeout=1";

    [Fact]
    public async Task PollSnapshotsEveryLiveToolAndSkipsTheRest() {
        var service = Build();

        await service.PollOnceAsync(CancellationToken.None);

        Assert.All(ToolCatalog.All.Where(t => t.Live), tool => Assert.NotNull(service.For(tool.Slug)));
        Assert.All(ToolCatalog.All.Where(t => !t.Live), tool => Assert.Null(service.For(tool.Slug)));
        Assert.All(service.Snapshot.Values, health => Assert.Null(health.RunningVersion));
    }

    [Fact]
    public async Task PollRaisesChangedOnce() {
        var service = Build();
        var raised = 0;
        service.Changed += () => raised++;

        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(1, raised);
    }

    private static ToolStatusService Build() {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        return new ToolStatusService(services, Registry(services));
    }

    private static ToolRegistry Registry(IServiceProvider services) =>
        new(new SettingsCache(new SettingsRegistry([], []), new SettingsStore(NpgsqlDataSource.Create(Unreachable), null)),
            services.GetRequiredService<ILogger<ToolRegistry>>());
}

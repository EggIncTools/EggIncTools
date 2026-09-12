using EggIdentity.Fallback;
using EggIdentity.Settings.Store;
using EggIncTools.Web.Components;
using Npgsql;

namespace EggIncTools.Web;

public static class Program {
    public static async Task Main(string[] args) {
#if DEBUG
        LocalSecrets.PromoteToEnvironment();
#endif
        var config = HostConfig.FromEnvironment();

        var builder = WebApplication.CreateBuilder(args);
        var runtime = HostServices.Register(builder, config);

        var app = builder.Build();
        ConfigurePipeline(app, config);
        await InitializeAsync(app, runtime);
        MapRoutes(app, config);

        await app.RunAsync();
    }

    private static void ConfigurePipeline(WebApplication app, HostConfig config) {
        app.UseForwardedHeaders();
        app.UseEggIdentityFallback();
        app.UseStaticFiles();

        if (config.AuthEnabled) {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.UseAntiforgery();
    }

    private static async Task InitializeAsync(WebApplication app, HostRuntime runtime) {
        if (runtime is not { SettingsStore: { } store, SettingsCache: { } cache, DataSource: { } source }) return;

        await store.MigrateAsync();

        var stopping = app.Lifetime.ApplicationStopping;
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SettingsChangeListener");
        _ = new SettingsChangeListener(source, cache).RunAsync(stopping)
            .ContinueWith(
                t => log.LogError(t.Exception, "settings change listener stopped; the settings cache will go stale"),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    private static void MapRoutes(WebApplication app, HostConfig config) {
        if (config.DatabaseEnabled) {
            app.MapGet("/health", async (NpgsqlDataSource db, CancellationToken ct) => {
                try {
                    await using var probe = db.CreateCommand("select 1");
                    await probe.ExecuteScalarAsync(ct);
                    return Results.Text("ok");
                } catch (NpgsqlException) {
                    return Results.Text("database unreachable", statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            });
        } else {
            app.MapGet("/health", () => Results.Text("ok"));
        }
        app.MapRazorComponents<AppHost>().AddInteractiveServerRenderMode();
    }
}

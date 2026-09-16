using EggIdentity.Fallback;
using EggIdentity.Settings.Store;
using EggIncTools.Shell;
using EggIncTools.Web.Components;
using EggIncTools.Web.Services;
using Npgsql;

namespace EggIncTools.Web;

public static class Program {
    public static async Task Main(string[] args) {
#if DEBUG
        LocalSecrets.PromoteToEnvironment();
#endif
        var config = HostConfig.FromEnvironment();

        var builder = WebApplication.CreateBuilder(args);
        LocalAdminGate.Guard(builder.Environment.EnvironmentName);
        var runtime = HostServices.Register(builder, config);

        var app = builder.Build();
        WarnIfAuthBypassed(app);
        ConfigurePipeline(app, config);
        await InitializeAsync(app, runtime);
        MapRoutes(app, config);

        await app.RunAsync();
    }

    private static void WarnIfAuthBypassed(WebApplication app) {
        if (!LocalAdminGate.IsOn(app.Environment.EnvironmentName)) return;

        var settings = LocalAdminGate.Settings();
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LocalAdmin").LogWarning(
            "AUTHENTICATION IS BYPASSED. {Enabled} is on in {Environment}, so every request is {User} "
            + "({UserId}) with role {Role} and no sign-in is required. Never run this on a public host.",
            LocalAdminGate.EnabledEnv, app.Environment.EnvironmentName, settings.Username,
            LocalAdminSettings.UserId, settings.RoleName);
    }

    private static void ConfigurePipeline(WebApplication app, HostConfig config) {
        var returns = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ReturnUrl");
        ToolCatalog.OnReturnRejected = host =>
            returns.LogWarning("rejected a returnUrl on {Host}; sending the user to {Hub} instead", host, ToolCatalog.HubUrl);

        app.UseForwardedHeaders();
        app.UseEggIdentityFallback();
        app.UseStaticFiles();

        if (config.AuthEnabled || LocalAdminGate.IsOn(app.Environment.EnvironmentName)) {
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

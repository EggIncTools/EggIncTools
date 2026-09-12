using EggIdentity.Auth;
using EggIdentity.Deploy;
using EggIdentity.Fallback;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using EggIncTools.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;

namespace EggIncTools.Web;

internal sealed record HostRuntime(
    NpgsqlDataSource? DataSource,
    SettingsStore? SettingsStore,
    SettingsCache? SettingsCache);

internal static class HostServices {
    public static HostRuntime Register(WebApplicationBuilder builder, HostConfig config) {
        builder.WebHost.UseUrls($"http://*:{config.Port}");
        builder.WebHost.UseStaticWebAssets();

        builder.Services.Configure<ForwardedHeadersOptions>(o => {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            o.ForwardLimit = null;
        });

        builder.Services.AddEggIdentityFallback(new FallbackBranding("Egg Inc Tools", BrandTokens.All));
        builder.Services.AddSingleton(config);

        NpgsqlDataSource? dataSource = null;
        var runtime = new HostRuntime(null, null, null);

        if (config.DatabaseEnabled) {
            dataSource = NpgsqlDataSource.Create(config.ConnString!);
            builder.Services.AddSingleton(_ => dataSource);
            runtime = RegisterSettings(builder, dataSource);
        }

        RegisterAuth(builder, config);
        RegisterStatus(builder, config);

        return runtime;
    }

    private static HostRuntime RegisterSettings(WebApplicationBuilder builder, NpgsqlDataSource dataSource) {
        var registry = SettingsRegistry.Compose(
            [HubSettings.Provider, SessionSettings.Provider], [DeployApps.Provider]);
        var store = new SettingsStore(dataSource, SecretProtector.FromEnvironment());
        var cache = new SettingsCache(registry, store);

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(cache);

        return new HostRuntime(dataSource, store, cache);
    }

    private static void RegisterAuth(WebApplicationBuilder builder, HostConfig config) {
        if (config.AuthEnabled && config.SessionOptions is { } session) {
            builder.Services.AddAuthentication(EggIdentitySessionDefaults.Scheme).AddEggIdentitySession(session);
        }

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddEggIdentityDeployFromEnvironment("egginctools");
    }

    private static void RegisterStatus(WebApplicationBuilder builder, HostConfig config) {
        if (!config.DatabaseEnabled) return;
        builder.Services.AddSingleton<ToolRegistry>();
        builder.Services.AddSingleton<ToolStatusService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ToolStatusService>());
    }
}

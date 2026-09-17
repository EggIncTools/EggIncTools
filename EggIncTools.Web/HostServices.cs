using System.Net.Http.Headers;
using EggIdentity.Auth;
using EggIdentity.Client;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Deploy.AdminUi;
using EggIdentity.Fallback;
using EggIdentity.Settings;
using EggIdentity.Settings.AdminUi;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;
using EggIncTools.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;

namespace EggIncTools.Web;

internal sealed record HostRuntime(
    NpgsqlDataSource? DataSource,
    SettingsStore? SettingsStore,
    SettingsCache? SettingsCache);

internal static class HostServices {
    private static readonly TimeSpan AdminApiTimeout = TimeSpan.FromSeconds(10);

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
            [HubSettings.Provider, SessionSettings.Provider],
            [DeployApps.Provider, DeployStacks.Provider, AdminTargets.Provider]);
        var store = new SettingsStore(dataSource, SecretProtector.FromEnvironment());
        var cache = new SettingsCache(registry, store);

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(cache);
        builder.Services.AddSingleton(new SettingsAdminService(registry, store, cache));
        builder.Services.AddSingleton<AdminTargetsService>();
        builder.Services.AddHttpClient<AdminApiClient>(http => http.Timeout = AdminApiTimeout);
        builder.Services.AddScoped<SettingsComparison>();

        return new HostRuntime(dataSource, store, cache);
    }

    private static void RegisterAuth(WebApplicationBuilder builder, HostConfig config) {
        if (LocalAdminGate.IsOn(builder.Environment.EnvironmentName)) {
            builder.Services.AddSingleton(LocalAdminGate.Settings());
            builder.Services.AddAuthentication(LocalAdminAuth.Scheme)
                .AddScheme<AuthenticationSchemeOptions, LocalAdminAuthenticationHandler>(LocalAdminAuth.Scheme, null);
        } else if (config.AuthEnabled && config.SessionOptions is { } session) {
            if (!config.IdentityApiEnabled) {
                throw new InvalidOperationException(
                    $"{HubSettings.ApiUrlEnv} and {HubSettings.ApiSecretEnv} are required when "
                    + "EGGIDENTITY_SESSION_SECRET enables session auth, because a revoked session is only detected "
                    + "by asking the identity API. Set both, or unset the session secret to run anonymous.");
            }

            builder.Services.AddHttpClient<IdentityApiClient>(c => {
                c.BaseAddress = new Uri(config.IdentityApiUrl!);
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.IdentityApiSecret);
            });
            builder.Services.AddAuthentication(EggIdentitySessionDefaults.Scheme).AddEggIdentitySession(session);
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(HubSettings.AdminPolicy, policy => policy.RequireAssertion(ctx => ctx.User.IsAtLeast(UserRole.Admin)));
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddEggIdentitySettingsPanel();
        builder.Services.AddEggIdentityPromotion();
        builder.Services.AddEggIdentityDeployToasts();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(o => o.DetailedErrors = !builder.Environment.IsProduction());
        builder.Services.AddEggIdentityDeployFromEnvironment("egginctools");
    }

    private static void RegisterStatus(WebApplicationBuilder builder, HostConfig config) {
        if (!config.DatabaseEnabled) return;
        builder.Services.AddSingleton<ToolRegistry>();
        builder.Services.AddSingleton<ToolStatusService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ToolStatusService>());
    }
}

using EggIdentity.Auth;

namespace EggIncTools.Web;

public sealed class HostConfig {
    public required string? ConnString { get; init; }
    public required bool HubOnly { get; init; }
    public required ushort Port { get; init; }
    public SessionCookieOptions? SessionOptions { get; init; }
    public required string? IdentityApiUrl { get; init; }
    public required string? IdentityApiSecret { get; init; }
    public bool AuthEnabled => SessionOptions is not null;

    public bool IdentityApiEnabled =>
        IdentityApiUrl is { Length: > 0 } && IdentityApiSecret is { Length: > 0 };

    public bool DatabaseEnabled => ConnString is { Length: > 0 };

    public static HostConfig FromEnvironment() {
        var hubOnly = Environment.GetEnvironmentVariable(HubSettings.HubOnlyEnv) is { } flag
            && (flag.Equals("true", StringComparison.OrdinalIgnoreCase) || flag == "1");

        var connString = Environment.GetEnvironmentVariable(HubSettings.ConnStringEnv);
        if (!hubOnly && string.IsNullOrEmpty(connString)) {
            throw new InvalidOperationException(
                $"{HubSettings.ConnStringEnv} is required unless {HubSettings.HubOnlyEnv} is set");
        }

        return new HostConfig {
            ConnString = connString,
            IdentityApiUrl = Environment.GetEnvironmentVariable(HubSettings.ApiUrlEnv),
            IdentityApiSecret = Environment.GetEnvironmentVariable(HubSettings.ApiSecretEnv),
            HubOnly = hubOnly,
            Port = ushort.TryParse(Environment.GetEnvironmentVariable(HubSettings.PortEnv), out var port) && port > 0
                ? port
                : HubSettings.DefaultPort,
            SessionOptions = SessionCookieOptions.FromEnvironment(),
        };
    }
}

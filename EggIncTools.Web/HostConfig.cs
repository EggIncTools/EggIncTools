using System.Diagnostics.CodeAnalysis;
using EggIdentity.Auth;

namespace EggIncTools.Web;

public sealed class HostConfig {
    public required string? ConnString { get; init; }
    public required bool HubOnly { get; init; }
    public required ushort Port { get; init; }
    public SessionCookieOptions? SessionOptions { get; init; }
    public required string? IdentityApiUrl { get; init; }
    public required string? IdentityApiSecret { get; init; }
    public string? BotConnString { get; init; }
    public bool AuthEnabled => SessionOptions is not null;
    public bool BotEnabled => BotConnString is { Length: > 0 } && IdentityApiEnabled;

    [MemberNotNullWhen(true, nameof(IdentityApiUrl), nameof(IdentityApiSecret))]
    public bool IdentityApiEnabled =>
        IdentityApiUrl is { Length: > 0 } && IdentityApiSecret is { Length: > 0 };

    [MemberNotNullWhen(true, nameof(ConnString))]
    public bool DatabaseEnabled => ConnString is { Length: > 0 };

    public static HostConfig FromEnvironment() {
        var hubOnly = HubSettings.FlagSet(HubSettings.HubOnlyEnv);

        var connString = Environment.GetEnvironmentVariable(HubSettings.ConnStringEnv);
        if (!hubOnly && string.IsNullOrEmpty(connString)) {
            throw new InvalidOperationException(
                $"{HubSettings.ConnStringEnv} is required unless {HubSettings.HubOnlyEnv} is set");
        }

        var botConnString = Environment.GetEnvironmentVariable(HubSettings.BotDbEnv);
        if (botConnString is { Length: > 0 } && botConnString == connString) {
            throw new InvalidOperationException(
                $"{HubSettings.BotDbEnv} must not be the identity database: the identity host's own bot already "
                + "owns bot_channel_config and deploy_state there. Point it at a database of its own.");
        }

        return new HostConfig {
            ConnString = connString,
            BotConnString = botConnString,
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

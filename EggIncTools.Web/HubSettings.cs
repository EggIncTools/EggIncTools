using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIncTools.Web;

public static class HubSettings {
    public const string ConnStringEnv = "IDENTITY_DB_CONNECTION";
    public const string PortEnv = "EGGINCTOOLS_PORT";
    public const string HubOnlyEnv = "EGGINCTOOLS_HUB_ONLY";
    public const ushort DefaultPort = 8090;

    private const string Core = "Core";
    private const string Build = "Build";
    private const string Deploy = "Deploy";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            "identity.db_connection", ConnStringEnv, "Postgres connection string", Core,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Shared EggIdentity database. Read before the settings store exists, so it stays on the stack.",
            Required = true,
        },
        new SettingDescriptor(
            "hub.port", PortEnv, "Listen port", Core,
            SettingKind.Number, ApplyTier.Bootstrap, Sensitivity.Plain) { Default = $"{DefaultPort}" },
        new SettingDescriptor(
            "hub.hub_only", HubOnlyEnv, "Hub-only mode", Core,
            SettingKind.Bool, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Serves the landing page with no database, auth or tool status. Makes the connection string optional.",
            Default = "false",
        },
        new SettingDescriptor(
            "build.git_sha", "GIT_SHA", "Build commit", Build,
            SettingKind.ReadOnly, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Stamped into the image at build time.",
        },
        new SettingDescriptor(
            "deploy.agent_url", DeployOptions.AgentUrlEnv, "Deploy agent URL", Deploy,
            SettingKind.Url, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Base URL of eggidentity-agent. Supplies the running version shown on each tool card.",
        },
    ]);
}

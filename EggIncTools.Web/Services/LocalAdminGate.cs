using EggIdentity.Contract;

namespace EggIncTools.Web.Services;

public sealed record LocalAdminSettings(UserRole Role) {
    public static readonly Guid UserId = new("00000000-0000-4000-8000-000000000002");

    public string RoleName => UserRoles.ToName(Role);

    public string Username => $"local-{RoleName}";
}

public static class LocalAdminGate {
    public const string EnabledEnv = "EGGINCTOOLS_LOCAL_ADMIN";
    public const string RoleEnv = "EGGINCTOOLS_LOCAL_ADMIN_ROLE";

    public static IReadOnlyList<string> AllowedEnvironments { get; } = ["Development", "Staging"];

    public static bool Requested =>
        Environment.GetEnvironmentVariable(EnabledEnv) is { } flag
        && (flag.Equals("true", StringComparison.OrdinalIgnoreCase) || flag == "1");

    public static bool IsOn(string environmentName) => Requested && IsAllowed(environmentName);

    public static void Guard(string environmentName) {
        if (!Requested || IsAllowed(environmentName)) return;

        throw new InvalidOperationException(
            $"{EnabledEnv} is set but ASPNETCORE_ENVIRONMENT is \"{environmentName}\". This switch bypasses "
            + $"authentication entirely and only loads in {string.Join(" or ", AllowedEnvironments)}. "
            + $"Unset {EnabledEnv} or fix ASPNETCORE_ENVIRONMENT.");
    }

    public static LocalAdminSettings Settings() {
        var role = Environment.GetEnvironmentVariable(RoleEnv);
        return new LocalAdminSettings(
            string.IsNullOrWhiteSpace(role) ? UserRole.Admin : UserRoles.Parse(role));
    }

    private static bool IsAllowed(string environmentName) =>
        AllowedEnvironments.Contains(environmentName, StringComparer.OrdinalIgnoreCase);
}

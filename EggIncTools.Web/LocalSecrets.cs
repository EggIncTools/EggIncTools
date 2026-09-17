namespace EggIncTools.Web;

internal static class LocalSecrets {
    public static void PromoteToEnvironment() {
        var secrets = new ConfigurationBuilder().AddUserSecrets<HostConfig>(optional: true).Build();

        foreach (var (key, value) in secrets.AsEnumerable()) {
            if (string.IsNullOrEmpty(value)) continue;
            if (Environment.GetEnvironmentVariable(key) is not null) continue;
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

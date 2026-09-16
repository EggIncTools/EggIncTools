using EggIdentity.Contract;
using EggIncTools.Web.Services;
using Xunit;

namespace EggIncTools.Web.Tests;

[Collection("LocalAdminGate")]
public sealed class LocalAdminGateTests : IDisposable {
    private readonly string? _enabled = Environment.GetEnvironmentVariable(LocalAdminGate.EnabledEnv);
    private readonly string? _role = Environment.GetEnvironmentVariable(LocalAdminGate.RoleEnv);

    public void Dispose() {
        Environment.SetEnvironmentVariable(LocalAdminGate.EnabledEnv, _enabled);
        Environment.SetEnvironmentVariable(LocalAdminGate.RoleEnv, _role);
    }

    [Fact]
    public void ItIsOffWhenTheVariableIsUnset() {
        Environment.SetEnvironmentVariable(LocalAdminGate.EnabledEnv, null);

        Assert.False(LocalAdminGate.Requested);
        Assert.False(LocalAdminGate.IsOn("Development"));
        Assert.False(LocalAdminGate.IsOn("Production"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("")]
    [InlineData("Prod")]
    [InlineData("Staging-2")]
    public void RequestingItOutsideTheAllowedEnvironmentsThrowsRatherThanBeingIgnored(string environment) {
        Environment.SetEnvironmentVariable(LocalAdminGate.EnabledEnv, "true");

        var thrown = Assert.Throws<InvalidOperationException>(() => LocalAdminGate.Guard(environment));

        Assert.Contains(LocalAdminGate.EnabledEnv, thrown.Message, StringComparison.Ordinal);
        Assert.False(LocalAdminGate.IsOn(environment));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("staging")]
    public void ItOnlyTurnsOnInAnAllowedEnvironment(string environment) {
        Environment.SetEnvironmentVariable(LocalAdminGate.EnabledEnv, "true");

        LocalAdminGate.Guard(environment);

        Assert.True(LocalAdminGate.IsOn(environment));
    }

    [Fact]
    public void AnUnsetVariableNeverThrowsWhateverTheEnvironment() {
        Environment.SetEnvironmentVariable(LocalAdminGate.EnabledEnv, null);

        LocalAdminGate.Guard("Production");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData(" ")]
    public void OnlyAnExplicitTrueOrOneCountsAsRequested(string value) {
        Environment.SetEnvironmentVariable(LocalAdminGate.EnabledEnv, value);

        Assert.False(LocalAdminGate.Requested);
        Assert.False(LocalAdminGate.IsOn("Development"));
        LocalAdminGate.Guard("Production");
    }

    [Fact]
    public void TheDefaultRoleIsAdminAndAnExplicitRoleIsHonoured() {
        Environment.SetEnvironmentVariable(LocalAdminGate.RoleEnv, null);
        Assert.Equal(UserRole.Admin, LocalAdminGate.Settings().Role);

        Environment.SetEnvironmentVariable(LocalAdminGate.RoleEnv, "Viewer");
        Assert.Equal(UserRole.Viewer, LocalAdminGate.Settings().Role);
    }

    [Fact]
    public void ProductionIsNotAnAllowedEnvironment() {
        Assert.DoesNotContain("Production", LocalAdminGate.AllowedEnvironments, StringComparer.OrdinalIgnoreCase);
    }
}

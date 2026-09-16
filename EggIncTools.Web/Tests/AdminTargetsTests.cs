using EggIdentity.Settings.Api;
using Xunit;

namespace EggIncTools.Web.Tests;

public class AdminTargetsTests {
    [Fact]
    public void AnAppWithNoSecretOnThisHostIsNotAdministrableAndSaysWhy() {
        var status = Describe("eggledger", "http://eggledger:5015", env: null);

        Assert.False(status.Administrable);
        Assert.Null(status.Target);
        Assert.Contains("ADMIN_SECRET_EGGLEDGER", status.Unavailable);
    }

    [Fact]
    public void AWhitespaceSecretCountsAsAbsent() {
        var status = Describe("eggledger", "http://eggledger:5015", env: "   ");

        Assert.False(status.Administrable);
    }

    [Fact]
    public void AnAppWithItsSecretResolvesToATargetCarryingTheInternalUrl() {
        var status = Describe("eggledger", "http://eggledger:5015", env: "s3cret");

        Assert.True(status.Administrable);
        Assert.Equal("eggledger", status.Target!.App);
        Assert.Equal("http://eggledger:5015/", status.Target.BaseUrl.AbsoluteUri);
    }

    [Fact]
    public void ADisabledRowStaysVisibleWithItsReasonRatherThanDisappearing() {
        var row = new AdminTargetRow {
            Name = "eggledger",
            AdminBaseUrl = "http://eggledger:5015",
            Enabled = false,
        };

        var status = AdminTargets.Describe(row, _ => "s3cret");

        Assert.False(status.Administrable);
        Assert.Equal("eggledger", status.App);
        Assert.NotNull(status.Unavailable);
    }

    [Fact]
    public void TheSecretEnvKeyIsDerivedFromTheAppNameNotStoredInTheDatabase() {
        Assert.Equal("ADMIN_SECRET_EGGLEDGER", AdminTargets.SecretEnvKey("eggledger"));
        Assert.Equal("ADMIN_SECRET_EGG_LEDGER", AdminTargets.SecretEnvKey("egg-ledger"));
    }

    [Fact]
    public void TheAdminTargetsCollectionHasNoFieldThatCouldHoldASecret() {
        var names = AdminTargets.Descriptor.Fields.Select(f => f.Name).ToList();

        Assert.Equal(["name", "admin_base_url", "enabled"], names);
        Assert.DoesNotContain(AdminTargets.Descriptor.Fields, f => f.IsSecret);
    }

    [Fact]
    public void NoStringOnTheStatusEverCarriesTheSecretItself() {
        const string secret = "s3cret-value-that-must-not-appear";
        var status = Describe("eggledger", "http://eggledger:5015", env: secret);

        var strings = typeof(AdminTargetStatus)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (string?)p.GetValue(status));

        Assert.All(strings, text => Assert.DoesNotContain(secret, text ?? ""));
    }

    private static AdminTargetStatus Describe(string name, string url, string? env) =>
        AdminTargets.Describe(
            new AdminTargetRow { Name = name, AdminBaseUrl = url },
            _ => env);
}

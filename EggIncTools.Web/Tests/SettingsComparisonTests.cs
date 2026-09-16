using EggIdentity.Contract;
using EggIdentity.Settings.Api;
using EggIncTools.Web.Services;
using Xunit;

namespace EggIncTools.Web.Tests;

public class SettingsComparisonTests {
    [Fact]
    public void ARowIsDivergentWhenTwoAppsHoldDifferentValues() {
        var row = Row("session.cookie_domain", secret: false,
            Cell("eggledger", "legacy.example"),
            Cell("eggabacus", "egginc.tools"));

        Assert.True(row.Divergent);
    }

    [Fact]
    public void ARowIsNotDivergentWhenEveryAppAgrees() {
        var row = Row("session.cookie_domain", secret: false,
            Cell("eggledger", "egginc.tools"),
            Cell("eggabacus", "egginc.tools"));

        Assert.False(row.Divergent);
    }

    [Fact]
    public void AnUnsetValueDoesNotCountAsADifference() {
        var row = Row("hub.port", secret: false,
            Cell("eggledger", "8090"),
            Cell("eggabacus", null));

        Assert.False(row.Divergent);
        Assert.Equal(1, row.SetCount);
    }

    [Fact]
    public void SecretsAreNeverReportedAsDivergentBecauseEveryValueArrivesMasked() {
        var row = Row("identity.db_connection", secret: true,
            Cell("eggledger", "********"),
            Cell("eggabacus", "********"));

        Assert.False(row.Divergent);
    }

    [Fact]
    public void TwoSecretsThatActuallyDifferStillCannotBeCompared() {
        var row = Row("identity.db_connection", secret: true,
            Cell("eggledger", "********"),
            Cell("eggabacus", "********"));

        Assert.True(row.Set.All(c => c.Display == "********"));
        Assert.False(row.Divergent);
    }

    [Fact]
    public void AnUnreachableAppReportsItsReasonRatherThanVanishing() {
        var status = AdminTargets.Describe(
            new AdminTargetRow { Name = "eggledger", AdminBaseUrl = "http://eggledger:5015" },
            _ => null);

        var app = new CompareApp(status, null);

        Assert.False(app.Ok);
        Assert.Equal("eggledger", app.App);
        Assert.Contains("ADMIN_SECRET_EGGLEDGER", app.Unavailable);
    }

    [Fact]
    public void ATransportFailureIsReportedAgainstTheAppNotSwallowed() {
        var status = AdminTargets.Describe(
            new AdminTargetRow { Name = "eggledger", AdminBaseUrl = "http://eggledger:5015" },
            _ => "secret");

        var app = new CompareApp(status, "connection refused");

        Assert.True(status.Administrable);
        Assert.False(app.Ok);
        Assert.Equal("connection refused", app.Unavailable);
    }

    [Fact]
    public void NoCompareCellEverCarriesARawSecret() {
        var wire = new AdminSettingWire {
            Key = "identity.db_connection",
            Label = "Postgres connection string",
            Category = "Core",
            Secret = true,
            Display = "********",
        };

        Assert.Equal("********", wire.Display);
    }

    private static CompareRow Row(string key, bool secret, params CompareCell[] cells) =>
        new(key, key, "Core", secret, cells);

    private static CompareCell Cell(string app, string? display) =>
        new(app, display, display is null ? null : "Database", display is { Length: > 0 });
}

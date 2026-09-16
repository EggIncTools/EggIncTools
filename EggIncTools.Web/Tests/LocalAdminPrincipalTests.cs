using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIncTools.Web.Services;
using Xunit;

namespace EggIncTools.Web.Tests;

public class LocalAdminPrincipalTests {
    [Fact]
    public void TheMintedPrincipalSatisfiesTheAdminPolicy() {
        var user = Principal(UserRole.Admin);

        Assert.True(user.IsAtLeast(UserRole.Admin));
        Assert.Equal(LocalAdminSettings.UserId, user.EggIdentityUserId());
    }

    [Fact]
    public void ALowerRoleDoesNotSilentlyBecomeAdmin() {
        var user = Principal(UserRole.Viewer);

        Assert.False(user.IsAtLeast(UserRole.Admin));
        Assert.True(user.IsAtLeast(UserRole.Viewer));
    }

    [Fact]
    public void TheLocalUserIdIsNotAnyRealUsersId() {
        Assert.NotEqual(Guid.Empty, LocalAdminSettings.UserId);
        Assert.StartsWith("00000000-0000-", LocalAdminSettings.UserId.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheUsernameSaysItIsLocal() {
        Assert.Equal("local-admin", new LocalAdminSettings(UserRole.Admin).Username);
    }

    private static ClaimsPrincipal Principal(UserRole role) {
        var settings = new LocalAdminSettings(role);
        return new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtRegisteredClaimNames.Sub, LocalAdminSettings.UserId.ToString()),
            new Claim(SessionClaims.Role, settings.RoleName),
        ], LocalAdminAuth.Scheme));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using EggIdentity.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EggIncTools.Web.Services;

public static class LocalAdminAuth {
    public const string Scheme = "LocalAdmin";
}

public sealed class LocalAdminAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    LocalAdminSettings settings)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        Claim[] claims = [
            new(JwtRegisteredClaimNames.Sub, LocalAdminSettings.UserId.ToString()),
            new(ClaimTypes.Name, settings.Username),
            new(SessionClaims.Name, settings.Username),
            new(SessionClaims.Role, settings.RoleName),
        ];

        var identity = new ClaimsIdentity(claims, LocalAdminAuth.Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), LocalAdminAuth.Scheme)));
    }
}

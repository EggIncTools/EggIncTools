using EggIdentity.Auth;
using EggIdentity.Deploy;

namespace EggIncTools.Web.Services;

public sealed class SessionTokenSource(IHttpContextAccessor accessor, SessionCookieOptions session) : ISessionTokenSource {
    private readonly string? _token =
        accessor.HttpContext?.Request.Cookies.TryGetValue(session.CookieName, out var token) == true ? token : null;

    public ValueTask<string?> GetAsync(CancellationToken ct) => ValueTask.FromResult(_token);
}

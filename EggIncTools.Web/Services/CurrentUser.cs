using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Components.Authorization;

namespace EggIncTools.Web.Services;

public sealed class CurrentUser(AuthenticationStateProvider auth) {
    public async Task<bool> IsAtLeastAsync(UserRole role) =>
        (await auth.GetAuthenticationStateAsync()).User.IsAtLeast(role);

    public Task<bool> IsAdminAsync() => IsAtLeastAsync(UserRole.Admin);
}

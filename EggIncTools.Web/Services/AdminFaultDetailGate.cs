using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.UI;

namespace EggIncTools.Web.Services;

public sealed class AdminFaultDetailGate(ICurrentUser user) : IFaultDetailGate {
    public async ValueTask<bool> CanSeeDetailAsync() => (await user.GetAsync()).IsAtLeast(UserRole.Admin);
}

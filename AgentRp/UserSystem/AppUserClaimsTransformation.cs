using System.Security.Claims;
using AgentRp.Data;
using Microsoft.AspNetCore.Authentication;

namespace AgentRp.UserSystem;

public sealed class AppUserClaimsTransformation(IAppUserResolver appUserResolver) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || principal.HasClaim(claim => claim.Type == ClaimTypes.Role && claim.Value is UserRoles.Admin or UserRoles.SuperUser or UserRoles.User))
        {
            return principal;
        }

        var user = await appUserResolver.ResolveAsync(principal);
        var identity = new ClaimsIdentity("AgentRpRoles");
        foreach (var role in user.Roles)
            identity.AddClaim(new(ClaimTypes.Role, role));

        identity.AddClaim(new("app_user_id", user.Id.ToString("D")));
        principal.AddIdentity(identity);
        return principal;
    }
}

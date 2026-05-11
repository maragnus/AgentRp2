using System.Security.Claims;
using AgentRp.Data;
using Microsoft.AspNetCore.Components.Authorization;

namespace AgentRp.UserSystem;

public interface ICurrentAppUserAccessor
{
    Task<CurrentAppUser> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public sealed record CurrentAppUser(
    Guid Id,
    string Email,
    string NormalizedEmail,
    string DisplayName,
    IReadOnlySet<string> Roles)
{
    public bool IsAdmin => Roles.Contains(UserRoles.Admin);
    public bool IsSuperUser => Roles.Contains(UserRoles.SuperUser);
    public bool CanInspectGenerationProcess => IsAdmin || IsSuperUser;
}

public sealed class CurrentAppUserAccessor(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor,
    IAppUserResolver appUserResolver) : ICurrentAppUserAccessor
{
    public async Task<CurrentAppUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var principal = await ResolvePrincipalAsync();
        return await appUserResolver.ResolveAsync(principal, cancellationToken);
    }

    async Task<ClaimsPrincipal> ResolvePrincipalAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
            return httpContext.User;

        return (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
    }
}

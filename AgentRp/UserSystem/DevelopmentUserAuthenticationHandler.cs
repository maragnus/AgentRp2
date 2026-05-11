using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AgentRp.UserSystem;

public sealed class DevelopmentUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", "development-user"),
            new Claim("provider", AppAuthenticationConstants.DevelopmentProviderKey),
            new Claim("email_verified", "true"),
            new Claim(ClaimTypes.NameIdentifier, "development-user"),
            new Claim(ClaimTypes.Name, "Development User"),
            new Claim(ClaimTypes.Email, "dev.user@local"),
            new Claim("email", "dev.user@local"),
            new Claim("name", "Development User")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

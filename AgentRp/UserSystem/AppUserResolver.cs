using System.Security.Claims;
using AgentRp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentRp.UserSystem;

public interface IAppUserResolver
{
    Task<CurrentAppUser> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public sealed class AppUserResolver(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IHostEnvironment hostEnvironment,
    IOptions<AuthOptions> authOptions,
    ILogger<AppUserResolver> logger) : IAppUserResolver
{
    static readonly SemaphoreSlim CreationGate = new(1, 1);

    public async Task<CurrentAppUser> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var identity = ResolveIdentity(principal, cancellationToken);
        await CreationGate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await LoadOrCreateUserAsync(dbContext, identity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToCurrentUser(user);
        }
        finally
        {
            CreationGate.Release();
        }
    }

    ExternalIdentitySnapshot ResolveIdentity(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("An authenticated user is required.");

        var providerKey = Claim(principal, "provider")
            ?? Claim(principal, "idp")
            ?? (hostEnvironment.IsDevelopment()
                ? AppAuthenticationConstants.DevelopmentProviderKey
                : authOptions.Value.ProviderKey);
        var subject = Claim(principal, "sub")
            ?? Claim(principal, ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The authenticated user is missing a subject identifier.");
        var issuer = Claim(principal, "iss") ?? providerKey;
        var tenantId = Claim(principal, "tid")
            ?? Claim(principal, "http://schemas.microsoft.com/identity/claims/tenantid")
            ?? "";
        var email = Claim(principal, "email")
            ?? Claim(principal, "preferred_username")
            ?? Claim(principal, ClaimTypes.Email)
            ?? "";
        var normalizedEmail = NormalizeEmail(email);
        var emailVerified = IsEmailVerified(principal);
        var displayName = Claim(principal, "name")
            ?? Claim(principal, ClaimTypes.Name)
            ?? email
            ?? subject;

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new InvalidOperationException("The authenticated user is missing an email address.");

        if (!hostEnvironment.IsDevelopment() && !emailVerified)
            throw new InvalidOperationException("The authenticated user must have a verified email address.");

        var resolvedProviderKey = providerKey.Trim();
        var resolvedIssuer = issuer.Trim();
        var resolvedSubject = subject.Trim();
        var resolvedTenantId = tenantId.Trim();
        var resolvedEmail = (email ?? string.Empty).Trim();
        var resolvedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(resolvedDisplayName))
            resolvedDisplayName = resolvedEmail;

        cancellationToken.ThrowIfCancellationRequested();
        return new(resolvedProviderKey, resolvedIssuer, resolvedSubject, resolvedTenantId, resolvedEmail, normalizedEmail, emailVerified, resolvedDisplayName);
    }

    async Task<UserRow> LoadOrCreateUserAsync(RpDbContext dbContext, ExternalIdentitySnapshot identity, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var existingIdentity = await dbContext.UserExternalIdentities
            .Include(row => row.User)
            .ThenInclude(user => user!.Roles)
            .AsSplitQuery()
            .Where(row => row.Issuer == identity.Issuer && row.Subject == identity.Subject)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingIdentity?.User is not null)
        {
            EnsureUserEnabled(existingIdentity.User);
            ApplyIdentity(existingIdentity, identity, now);
            ApplyUser(existingIdentity.User, identity, now);
            EnsureDefaultRoles(existingIdentity.User, identity, now);
            return existingIdentity.User;
        }

        var matchingUsers = await dbContext.Users
            .Include(user => user.Roles)
            .AsSplitQuery()
            .Where(user => user.NormalizedEmail == identity.NormalizedEmail && user.EmailVerified)
            .OrderBy(user => user.CreatedUtc)
            .ToListAsync(cancellationToken);
        if (matchingUsers.Count > 1)
            throw new InvalidOperationException("Signing in failed because more than one user already has this verified email address.");

        var user = matchingUsers.FirstOrDefault() ?? new()
        {
            Id = Guid.NewGuid(),
            CreatedUtc = now
        };
        EnsureUserEnabled(user);
        if (matchingUsers.Count == 0)
            dbContext.Users.Add(user);

        ApplyUser(user, identity, now);
        EnsureDefaultRoles(user, identity, now);
        var identityRow = new UserExternalIdentityRow
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedUtc = now,
            User = user
        };
        dbContext.UserExternalIdentities.Add(identityRow);
        ApplyIdentity(identityRow, identity, now);
        logger.LogInformation("Linked identity {Issuer}/{Subject} to user {UserId}.", identity.Issuer, identity.Subject, user.Id);
        return user;
    }

    static void EnsureUserEnabled(UserRow user)
    {
        if (user.DisabledUtc is not null)
            throw new InvalidOperationException("Signing in failed because this user is disabled.");
    }

    void EnsureDefaultRoles(UserRow user, ExternalIdentitySnapshot identity, DateTime now)
    {
        EnsureRole(user, UserRoles.User, now);
        var bootstrapAdmins = authOptions.Value.BootstrapAdminEmails
            .Select(NormalizeEmail)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (bootstrapAdmins.Contains(identity.NormalizedEmail))
            EnsureRole(user, UserRoles.Admin, now);
    }

    static void EnsureRole(UserRow user, string role, DateTime now)
    {
        if (!UserRoles.All.Contains(role) || user.Roles.Any(row => row.Role == role))
            return;

        user.Roles.Add(new()
        {
            UserId = user.Id,
            Role = role,
            CreatedUtc = now
        });
    }

    static void ApplyUser(UserRow user, ExternalIdentitySnapshot identity, DateTime now)
    {
        user.Email = identity.Email;
        user.NormalizedEmail = identity.NormalizedEmail;
        user.EmailVerified = identity.EmailVerified;
        user.DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? identity.Email : identity.DisplayName;
        user.UpdatedUtc = now;
        user.LastSeenUtc = now;
    }

    static void ApplyIdentity(UserExternalIdentityRow row, ExternalIdentitySnapshot identity, DateTime now)
    {
        row.ProviderKey = identity.ProviderKey;
        row.Issuer = identity.Issuer;
        row.Subject = identity.Subject;
        row.TenantId = identity.TenantId;
        row.Email = identity.Email;
        row.NormalizedEmail = identity.NormalizedEmail;
        row.EmailVerified = identity.EmailVerified;
        row.UpdatedUtc = now;
        row.LastSeenUtc = now;
    }

    static CurrentAppUser ToCurrentUser(UserRow user) =>
        new(
            user.Id,
            user.Email,
            user.NormalizedEmail,
            user.DisplayName,
            user.Roles.Select(role => role.Role).ToHashSet(StringComparer.Ordinal));

    static string? Claim(ClaimsPrincipal principal, string type) =>
        principal.FindFirst(type)?.Value;

    static string NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? "" : email.Trim().ToUpperInvariant();

    static bool IsEmailVerified(ClaimsPrincipal principal)
    {
        var value = Claim(principal, "email_verified")
            ?? Claim(principal, "emails_verified")
            ?? "";
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    sealed record ExternalIdentitySnapshot(
        string ProviderKey,
        string Issuer,
        string Subject,
        string TenantId,
        string Email,
        string NormalizedEmail,
        bool EmailVerified,
        string DisplayName);
}

namespace AgentRp.Data;

public sealed class UserRow
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public bool EmailVerified { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public DateTime? DisabledUtc { get; set; }
    public List<UserExternalIdentityRow> ExternalIdentities { get; set; } = [];
    public List<UserRoleRow> Roles { get; set; } = [];
}

public sealed class UserExternalIdentityRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserRow? User { get; set; }
    public string ProviderKey { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public bool EmailVerified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

public sealed class UserRoleRow
{
    public Guid UserId { get; set; }
    public UserRow? User { get; set; }
    public string Role { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string SuperUser = "SuperUser";
    public const string User = "User";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Admin,
        SuperUser,
        User
    };
}

using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public sealed partial class RpDbContext
{
    static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRow>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email).HasMaxLength(320);
            builder.Property(x => x.NormalizedEmail).HasMaxLength(320);
            builder.Property(x => x.DisplayName).HasMaxLength(256);
            builder.HasIndex(x => x.NormalizedEmail);
            builder.HasMany(x => x.ExternalIdentities)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Roles)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserExternalIdentityRow>(builder =>
        {
            builder.ToTable("UserExternalIdentities");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ProviderKey).HasMaxLength(64);
            builder.Property(x => x.Issuer).HasMaxLength(500);
            builder.Property(x => x.Subject).HasMaxLength(256);
            builder.Property(x => x.TenantId).HasMaxLength(128);
            builder.Property(x => x.Email).HasMaxLength(320);
            builder.Property(x => x.NormalizedEmail).HasMaxLength(320);
            builder.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
            builder.HasIndex(x => new { x.ProviderKey, x.Subject }).IsUnique();
            builder.HasIndex(x => x.NormalizedEmail);
        });

        modelBuilder.Entity<UserRoleRow>(builder =>
        {
            builder.ToTable("UserRoles");
            builder.HasKey(x => new { x.UserId, x.Role });
            builder.Property(x => x.Role).HasMaxLength(64);
            builder.HasIndex(x => x.Role);
        });
    }
}

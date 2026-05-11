using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Data;
using AgentRp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using AgentRp.UserSystem;

namespace AgentRp.Tests;

public sealed class CurrentAppUserAccessorTests
{
	private sealed class TestDbContextFactory : IDbContextFactory<RpDbContext>
	{
		private readonly DbContextOptions<RpDbContext> _options = new DbContextOptionsBuilder<RpDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;

		public RpDbContext CreateDbContext()
		{
			return new RpDbContext(_options);
		}

		public Task<RpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(CreateDbContext());
		}
	}

	private sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
	{
		public override Task<AuthenticationState> GetAuthenticationStateAsync()
		{
			return Task.FromResult(new AuthenticationState(principal));
		}
	}

	private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = environmentName;

		public string ApplicationName { get; set; } = "AgentRp.Tests";

		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

		public IFileProvider ContentRootFileProvider { get; set; } = null!;
	}

	[Fact]
	public async Task DevelopmentUserCreatesAndReusesInternalUserWithBootstrapAdminRole()
	{
		TestDbContextFactory factory = new TestDbContextFactory();
		CurrentAppUserAccessor accessor = NewAccessor(factory, DevelopmentPrincipal("dev.user@local", "development-user"), Environments.Development, new string[1] { "dev.user@local" });
		CurrentAppUser first = await accessor.GetCurrentUserAsync();
		Assert.Equal(actual: (await accessor.GetCurrentUserAsync()).Id, expected: first.Id);
		Assert.True(first.IsAdmin);
		Assert.Contains("User", first.Roles);
		await using RpDbContext dbContext = await factory.CreateDbContextAsync();
		Assert.Equal(1, await dbContext.Users.CountAsync());
		Assert.Equal(1, await dbContext.UserExternalIdentities.CountAsync());
	}

	[Fact]
	public async Task ProductionClaimsRequireVerifiedEmail()
	{
		CurrentAppUserAccessor accessor = NewAccessor(new TestDbContextFactory(), ExternalPrincipal("entra", "subject-1", "person@example.com", emailVerified: false), Environments.Production);
		Assert.Contains("verified email", (await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.GetCurrentUserAsync())).Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ClaimsTransformationUsesProvidedMiddlewarePrincipal()
	{
		TestDbContextFactory factory = new TestDbContextFactory();
		ClaimsPrincipal principal = DevelopmentPrincipal("dev.user@local", "development-user");
		AppUserClaimsTransformation transformation = new AppUserClaimsTransformation(NewResolver(factory, Environments.Development, new string[1] { "dev.user@local" }));
		ClaimsPrincipal transformed = await transformation.TransformAsync(principal);
		Assert.Contains(transformed.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == UserRoles.Admin);
		Assert.Contains(transformed.Claims, claim => claim.Type == "app_user_id");
	}

	[Fact]
	public async Task VerifiedEmailLinksSecondExternalIdentityToExistingUser()
	{
		TestDbContextFactory factory = new TestDbContextFactory();
		CurrentAppUserAccessor first = NewAccessor(factory, ExternalPrincipal("google", "google-subject", "person@example.com", emailVerified: true), Environments.Production);
		CurrentAppUserAccessor second = NewAccessor(factory, ExternalPrincipal("microsoft", "microsoft-subject", "PERSON@example.com", emailVerified: true), Environments.Production);
		CurrentAppUser firstUser = await first.GetCurrentUserAsync();
		Assert.Equal(actual: (await second.GetCurrentUserAsync()).Id, expected: firstUser.Id);
		await using RpDbContext dbContext = await factory.CreateDbContextAsync();
		Assert.Equal(1, await dbContext.Users.CountAsync());
		List<string> identities = await (from identity in dbContext.UserExternalIdentities
			orderby identity.ProviderKey
			select string.Format("{0}:{1}:{2}:{3}", new object[4] { identity.ProviderKey, identity.Issuer, identity.Subject, identity.UserId })).ToListAsync();
		Assert.True(identities.Count == 2, string.Join("; ", identities));
	}

	[Fact]
	public async Task DuplicateVerifiedEmailUsersFailClosed()
	{
		TestDbContextFactory factory = new TestDbContextFactory();
		await using (RpDbContext dbContext = await factory.CreateDbContextAsync())
		{
			dbContext.Users.Add(NewUser("person@example.com"));
			dbContext.Users.Add(NewUser("PERSON@example.com"));
			await dbContext.SaveChangesAsync();
		}
		CurrentAppUserAccessor accessor = NewAccessor(factory, ExternalPrincipal("entra", "subject-1", "person@example.com", emailVerified: true), Environments.Production);
		Assert.Contains("more than one user", (await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.GetCurrentUserAsync())).Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task DisabledExistingUserFailsClosed()
	{
		TestDbContextFactory factory = new TestDbContextFactory();
		await using (RpDbContext dbContext = await factory.CreateDbContextAsync())
		{
			UserRow user = NewUser("person@example.com");
			user.DisabledUtc = DateTime.UtcNow;
			dbContext.Users.Add(user);
			await dbContext.SaveChangesAsync();
		}
		CurrentAppUserAccessor accessor = NewAccessor(factory, ExternalPrincipal("entra", "subject-1", "person@example.com", emailVerified: true), Environments.Production);
		Assert.Contains("disabled", (await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.GetCurrentUserAsync())).Message, StringComparison.OrdinalIgnoreCase);
	}

	private static CurrentAppUserAccessor NewAccessor(TestDbContextFactory factory, ClaimsPrincipal principal, string environmentName, string[]? bootstrapAdmins = null)
	{
		return new CurrentAppUserAccessor(new TestAuthenticationStateProvider(principal), new HttpContextAccessor(), NewResolver(factory, environmentName, bootstrapAdmins));
	}

	private static AppUserResolver NewResolver(TestDbContextFactory factory, string environmentName, string[]? bootstrapAdmins = null)
	{
		return new AppUserResolver(factory, new TestHostEnvironment(environmentName), Options.Create(new AuthOptions
		{
			BootstrapAdminEmails = (bootstrapAdmins?.ToList() ?? new List<string>())
		}), NullLogger<AppUserResolver>.Instance);
	}

	private static ClaimsPrincipal DevelopmentPrincipal(string email, string subject)
	{
		return Principal(new Claim[6]
		{
			new Claim("sub", subject),
			new Claim("provider", "development"),
			new Claim("email_verified", "true"),
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", email),
			new Claim("email", email),
			new Claim("name", "Development User")
		});
	}

	private static ClaimsPrincipal ExternalPrincipal(string issuer, string subject, string email, bool emailVerified)
	{
		return Principal(new Claim[7]
		{
			new Claim("iss", issuer),
			new Claim("sub", subject),
			new Claim("provider", issuer),
			new Claim("email_verified", emailVerified ? "true" : "false"),
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", email),
			new Claim("email", email),
			new Claim("name", "Person")
		});
	}

	private static ClaimsPrincipal Principal(IEnumerable<Claim> claims)
	{
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
	}

	private static UserRow NewUser(string email)
	{
		return new UserRow
		{
			Id = Guid.NewGuid(),
			Email = email,
			NormalizedEmail = email.Trim().ToUpperInvariant(),
			EmailVerified = true,
			DisplayName = email,
			CreatedUtc = DateTime.UtcNow,
			UpdatedUtc = DateTime.UtcNow
		};
	}
}

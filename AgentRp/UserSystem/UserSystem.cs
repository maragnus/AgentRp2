using AgentRp.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgentRp.UserSystem;

public static class UserSystem
{
    public static WebApplicationBuilder AddUserSystem(this WebApplicationBuilder builder)
    {
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AppAuthenticationConstants.CanManageSystemGlobalsPolicy, policy => policy.RequireRole(UserRoles.Admin));
            options.AddPolicy(AppAuthenticationConstants.CanInspectGenerationProcessPolicy, policy => policy.RequireRole(UserRoles.Admin, UserRoles.SuperUser));
            options.AddPolicy(AppAuthenticationConstants.CanViewPromptsPolicy, policy => policy.RequireRole(UserRoles.Admin, UserRoles.SuperUser));
            options.AddPolicy(AppAuthenticationConstants.CanAccessStoryPolicy, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(AppAuthenticationConstants.CanManageUsersPolicy, policy => policy.RequireRole(UserRoles.Admin));
        });
        builder.Services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
        builder.Services.AddOptions<AuthOptions>()
            .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
            .ValidateOnStart();
        builder.ConfigureAuthentication();
        builder.Services.AddScoped<IAppUserResolver, AppUserResolver>();
        builder.Services.AddScoped<ICurrentAppUserAccessor, CurrentAppUserAccessor>();
        builder.Services.AddScoped<IAppAuthorizationService, AppAuthorizationService>();
        builder.Services.AddScoped<IClaimsTransformation, AppUserClaimsTransformation>();
        return builder;
    }

    public static WebApplication MapUserSystem(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/login", (HttpContext httpContext) =>
        {
            var returnUrl = GetLocalReturnUrl(httpContext.Request.Query["returnUrl"].ToString());
            if (app.Environment.IsDevelopment())
                return Results.Redirect(returnUrl);

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        app.MapGet("/logout", (HttpContext httpContext) =>
        {
            var returnUrl = GetLocalReturnUrl(httpContext.Request.Query["returnUrl"].ToString());
            var properties = new AuthenticationProperties { RedirectUri = returnUrl };
            return app.Environment.IsDevelopment()
                ? Results.SignOut(properties, [AppAuthenticationConstants.DevelopmentScheme])
                : Results.SignOut(properties, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        return app;
    }

    static void ConfigureAuthentication(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AppAuthenticationConstants.DevelopmentScheme;
                    options.DefaultChallengeScheme = AppAuthenticationConstants.DevelopmentScheme;
                    options.DefaultScheme = AppAuthenticationConstants.DevelopmentScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, DevelopmentUserAuthenticationHandler>(
                    AppAuthenticationConstants.DevelopmentScheme,
                    _ => { });
            return;
        }

        var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? throw new InvalidOperationException("Auth configuration could not be loaded.");
        var entra = authOptions.Providers.EntraExternal;
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-AgentRp";
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.SlidingExpiration = true;
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = entra.Authority;
                options.ClientId = entra.ClientId;
                options.ClientSecret = entra.ClientSecret;
                options.ResponseType = "code";
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "roles",
                    ValidateIssuer = true
                };
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        var tenantId = context.Principal?.FindFirst("tid")?.Value;
                        if (string.IsNullOrWhiteSpace(tenantId)
                            || !entra.AllowedTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
                            context.Fail("The signed-in account is not from an allowed Microsoft Entra tenant.");

                        return Task.CompletedTask;
                    }
                };
            });
    }

    static string GetLocalReturnUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "/";

        return value[0] == '/' && (value.Length == 1 || value[1] != '/') && !value.StartsWith("/\\", StringComparison.Ordinal)
            ? value
            : "/";
    }
}

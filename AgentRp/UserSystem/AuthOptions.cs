using Microsoft.Extensions.Options;

namespace AgentRp.UserSystem;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "http://localhost";
    public string Audience { get; set; } = "agentrp";
    public string ProviderKey { get; set; } = AppAuthenticationConstants.EntraExternalProviderKey;
    public string ApiBaseUrl { get; set; } = "http://localhost";
    public List<string> BootstrapAdminEmails { get; set; } = [];
    public AuthProviderOptions Providers { get; set; } = new();
}

public sealed class AuthProviderOptions
{
    public EntraExternalAuthOptions EntraExternal { get; set; } = new();
}

public sealed class EntraExternalAuthOptions
{
    public string Authority { get; set; } = "http://localhost";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public List<string> AllowedTenants { get; set; } = [];
    public List<string> RedirectUris { get; set; } = [];
}

public sealed class AuthOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<AuthOptions>
{
    static readonly string[] NonDevelopmentPlaceholderTokens =
    [
        "your-app-hostname",
        "example.com",
        "{tenantId}",
        "your-tenant-id",
        "your-client-id",
        "your-client-secret"
    ];

    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var errors = new List<string>();

        ValidateHttpUrl(options.Issuer, "Auth:Issuer", errors);
        ValidateHttpUrl(options.ApiBaseUrl, "Auth:ApiBaseUrl", errors);

        if (string.IsNullOrWhiteSpace(options.Audience))
            errors.Add("Auth:Audience is required.");

        if (string.IsNullOrWhiteSpace(options.ProviderKey))
            errors.Add("Auth:ProviderKey is required.");

        if (hostEnvironment.IsDevelopment())
            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);

        var entra = options.Providers.EntraExternal;
        ValidateHttpUrl(entra.Authority, "Auth:Providers:EntraExternal:Authority", errors);

        if (string.IsNullOrWhiteSpace(entra.ClientId))
            errors.Add("Auth:Providers:EntraExternal:ClientId is required.");

        if (string.IsNullOrWhiteSpace(entra.ClientSecret))
            errors.Add("Auth:Providers:EntraExternal:ClientSecret is required.");

        if (entra.AllowedTenants.Count == 0)
            errors.Add("Auth:Providers:EntraExternal:AllowedTenants must contain at least one tenant ID.");

        if (entra.RedirectUris.Count == 0)
            errors.Add("Auth:Providers:EntraExternal:RedirectUris must contain at least one redirect URI.");

        foreach (var redirectUri in entra.RedirectUris)
            ValidateHttpUrl(redirectUri, "Auth:Providers:EntraExternal:RedirectUris", errors);

        ValidateNonDevelopmentValues(options, errors);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    static void ValidateNonDevelopmentValues(AuthOptions options, ICollection<string> errors)
    {
        ValidateNonDevelopmentUrl(options.Issuer, "Auth:Issuer", errors);
        ValidateNonDevelopmentUrl(options.ApiBaseUrl, "Auth:ApiBaseUrl", errors);
        ValidateNonDevelopmentUrl(options.Providers.EntraExternal.Authority, "Auth:Providers:EntraExternal:Authority", errors);

        if (ContainsPlaceholderToken(options.Providers.EntraExternal.ClientId))
            errors.Add("Auth:Providers:EntraExternal:ClientId must be replaced with the real Entra application client ID.");

        if (ContainsPlaceholderToken(options.Providers.EntraExternal.ClientSecret))
            errors.Add("Auth:Providers:EntraExternal:ClientSecret must be replaced with the real Entra application client secret.");

        if (options.Providers.EntraExternal.AllowedTenants.Any(string.IsNullOrWhiteSpace))
            errors.Add("Auth:Providers:EntraExternal:AllowedTenants must not contain blank values.");

        if (options.Providers.EntraExternal.AllowedTenants.Any(ContainsPlaceholderToken))
            errors.Add("Auth:Providers:EntraExternal:AllowedTenants must contain real tenant IDs.");

        foreach (var redirectUri in options.Providers.EntraExternal.RedirectUris)
            ValidateNonDevelopmentUrl(redirectUri, "Auth:Providers:EntraExternal:RedirectUris", errors);

        var expectedSigninRedirectUri = $"{NormalizeUrl(options.ApiBaseUrl)}/signin-oidc";
        if (!options.Providers.EntraExternal.RedirectUris.Any(uri => string.Equals(NormalizeUrl(uri), expectedSigninRedirectUri, StringComparison.OrdinalIgnoreCase)))
            errors.Add($"Auth:Providers:EntraExternal:RedirectUris must include '{expectedSigninRedirectUri}'.");
    }

    static void ValidateHttpUrl(string value, string settingName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{settingName} is required.");
            return;
        }

        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{settingName} must be an absolute HTTP or HTTPS URL.");
            return;
        }

        try
        {
            _ = new Uri(value);
        }
        catch (UriFormatException)
        {
            errors.Add($"{settingName} must be a valid HTTP or HTTPS URL.");
        }
    }

    static void ValidateNonDevelopmentUrl(string value, string settingName, ICollection<string> errors)
    {
        if (ContainsPlaceholderToken(value))
        {
            errors.Add($"{settingName} still contains a placeholder value.");
            return;
        }

        try
        {
            var uri = new Uri(value);
            if (IsLocalDevelopmentHost(uri.Host))
                errors.Add($"{settingName} must use the public app hostname, not '{uri.Host}'.");
        }
        catch (UriFormatException)
        {
        }
    }

    static bool ContainsPlaceholderToken(string value) =>
        NonDevelopmentPlaceholderTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    static bool IsLocalDevelopmentHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);

    static string NormalizeUrl(string value) => value.Trim().TrimEnd('/');
}

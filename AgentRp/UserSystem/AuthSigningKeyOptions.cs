namespace AgentRp.UserSystem;

public sealed class AuthSigningKeyOptions
{
    public const string SectionName = "Auth:SigningKey";

    // Base64-encoded PFX (certificate + private key). In production, supply via secret store/env vars.
    public string? PfxBase64 { get; init; }

    public string? PfxPassword { get; init; }

    // Optional explicit KeyId (kid) for JWT headers. Defaults to certificate thumbprint.
    public string? KeyId { get; init; }
}


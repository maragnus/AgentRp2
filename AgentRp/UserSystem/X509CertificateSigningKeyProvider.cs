using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgentRp.UserSystem;

public sealed class X509CertificateSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly AuthSigningKeyOptions options;
    private readonly Lazy<X509Certificate2> certificate;

    public X509CertificateSigningKeyProvider(IOptions<AuthSigningKeyOptions> options)
    {
        this.options = options.Value;
        certificate = new Lazy<X509Certificate2>(LoadCertificate);
    }

    public SigningCredentials GetSigningCredentials()
    {
        var cert = certificate.Value;
        var key = new X509SecurityKey(cert) { KeyId = GetKeyId(cert) };
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public IReadOnlyCollection<SecurityKey> GetValidationKeys()
    {
        var cert = certificate.Value;
        return [new X509SecurityKey(cert) { KeyId = GetKeyId(cert) }];
    }

    private string GetKeyId(X509Certificate2 cert) =>
        !string.IsNullOrWhiteSpace(options.KeyId)
            ? options.KeyId.Trim()
            : cert.Thumbprint;

    private X509Certificate2 LoadCertificate()
    {
        if (string.IsNullOrWhiteSpace(options.PfxBase64))
            throw new InvalidOperationException(
                $"JWT signing key is not configured. Set '{AuthSigningKeyOptions.SectionName}:PfxBase64' (base64 PFX) in non-Development environments.");

        byte[] pfxBytes;
        try
        {
            pfxBytes = Convert.FromBase64String(options.PfxBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"'{AuthSigningKeyOptions.SectionName}:PfxBase64' must be valid base64.", ex);
        }

        // EphemeralKeySet avoids touching the filesystem and works well in containers.
        X509Certificate2 cert;
        try
        {
            cert = X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                options.PfxPassword,
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"'{AuthSigningKeyOptions.SectionName}:PfxBase64' must be a readable PFX for the configured password.", ex);
        }

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException(
                $"'{AuthSigningKeyOptions.SectionName}:PfxBase64' must include the certificate private key.");

        var now = DateTimeOffset.UtcNow;
        if (now < cert.NotBefore.ToUniversalTime())
            throw new InvalidOperationException(
                $"'{AuthSigningKeyOptions.SectionName}:PfxBase64' certificate is not valid until {cert.NotBefore:u}.");

        if (now >= cert.NotAfter.ToUniversalTime())
            throw new InvalidOperationException(
                $"'{AuthSigningKeyOptions.SectionName}:PfxBase64' certificate expired at {cert.NotAfter:u}.");

        return cert;
    }

    public void Dispose()
    {
        if (certificate.IsValueCreated)
            certificate.Value.Dispose();
    }
}

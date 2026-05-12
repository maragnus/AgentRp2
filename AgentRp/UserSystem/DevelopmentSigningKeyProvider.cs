using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AgentRp.UserSystem;

public sealed class DevelopmentSigningKeyProvider : ISigningKeyProvider
{
    private readonly SymmetricSecurityKey key;
    private readonly SigningCredentials signingCredentials;

    public DevelopmentSigningKeyProvider()
    {
        key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64))
        {
            KeyId = $"dev-{Guid.NewGuid():N}"
        };
        signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public SigningCredentials GetSigningCredentials() => signingCredentials;

    public IReadOnlyCollection<SecurityKey> GetValidationKeys() => [key];
}

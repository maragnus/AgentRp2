using Microsoft.IdentityModel.Tokens;

namespace AgentRp.UserSystem;

public interface ISigningKeyProvider
{
    SigningCredentials GetSigningCredentials();

    IReadOnlyCollection<SecurityKey> GetValidationKeys();
}

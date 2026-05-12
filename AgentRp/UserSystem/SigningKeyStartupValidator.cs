namespace AgentRp.UserSystem;

public sealed class SigningKeyStartupValidator(ISigningKeyProvider signingKeyProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = signingKeyProvider.GetSigningCredentials();
        _ = signingKeyProvider.GetValidationKeys();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

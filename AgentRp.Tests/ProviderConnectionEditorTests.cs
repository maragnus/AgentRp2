using AgentRp.Components.Providers;
using AgentRp.Components.Common;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class ProviderConnectionEditorTests
{
    [Fact]
    public void ValidationRejectsMissingRequiredKeyAndInvalidEndpoint()
    {
        var meta = new AiProviderMeta
        {
            Name = "Compatible",
            KeyLabel = "API Key",
            ApiKeyRequired = true,
            NeedsEndpoint = true,
            EndpointRequired = true
        };

        var missingKey = ProviderConnectionValidation.Validate(meta, new() { Name = "Provider", Endpoint = "https://example.com/v1" });
        var invalidEndpoint = ProviderConnectionValidation.Validate(meta, new() { Name = "Provider", ApiKey = "key", Endpoint = "example.com/v1" });

        Assert.False(missingKey.IsValid);
        Assert.False(invalidEndpoint.IsValid);
    }

    [Fact]
    public void DraftTestCloneDoesNotMutateSavedProvider()
    {
        var provider = new AiProvider
        {
            Id = "provider",
            Name = "Saved",
            Type = "compatible",
            ApiKey = "saved-key",
            Endpoint = "https://saved.example.com/v1"
        };
        var draft = ProviderConnectionDraft.FromProvider(provider);
        draft.Name = "Draft";
        draft.ApiKey = "draft-key";
        draft.Endpoint = "https://draft.example.com/v1";

        var clone = draft.CloneProvider(provider);

        Assert.Equal("Saved", provider.Name);
        Assert.Equal("saved-key", provider.ApiKey);
        Assert.Equal("https://saved.example.com/v1", provider.Endpoint);
        Assert.Equal("Draft", clone.Name);
        Assert.Equal("draft-key", clone.ApiKey);
        Assert.Equal("https://draft.example.com/v1", clone.Endpoint);
    }

    [Fact]
    public async Task EditorDisablesSaveWhenUnchangedAndTestWhenInvalid()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<IAiProviderConnectionService, TestProviderConnectionService>();
        await using var liveStore = new LiveRoleplayStore(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var session = new RoleplaySession(liveStore);
        await session.InitializeAsync();
        var provider = new AiProvider
        {
            Id = "provider",
            Name = "Compatible",
            Type = "compatible",
            Enabled = true,
            ApiKey = "key",
            Endpoint = "https://example.com/v1"
        };
        var meta = new AiProviderMeta
        {
            Id = "compatible",
            Name = "Compatible",
            KeyLabel = "API Key",
            NeedsEndpoint = true,
            EndpointRequired = true,
            ApiKeyRequired = true
        };

        var component = context.Render<ProviderConnectionEditor>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.Provider, provider)
            .Add(item => item.Meta, meta));

        var save = component.FindAll("button").First(button => button.TextContent.Contains("Save", StringComparison.Ordinal));
        Assert.True(save.HasAttribute("disabled"));

        var endpointInput = component.FindComponents<AppInput>().Single(input => input.Instance.Placeholder == "https://...");
        await endpointInput.InvokeAsync(() => endpointInput.Instance.NotifyTextValueChanged("not-a-url"));

        var test = component.FindAll("button").First(button => button.TextContent.Contains("Test connection", StringComparison.Ordinal));
        Assert.True(test.HasAttribute("disabled"));
    }

    sealed class TestProviderConnectionService : IAiProviderConnectionService
    {
        public Task TestProviderAsync(AiProvider provider, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<AiProviderModel>> DiscoverModelsAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AiProviderModel>());
    }
}

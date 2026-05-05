using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using System.Net;

namespace AgentRp.Tests;

public sealed class ProviderModelSelectionTests
{
    [Fact]
    public void TextSelectionRequiresUserChatEnablementAndTextCapability()
    {
        var providers = new List<AiProvider>
        {
            new()
            {
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "capable-not-enabled",
                        Enabled = true,
                        Text = false,
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    },
                    new()
                    {
                        Id = "enabled-not-capable",
                        Enabled = true,
                        Text = true,
                        Capabilities = new() { TextInput = false, TextOutput = false }
                    },
                    new()
                    {
                        Id = "enabled-capable",
                        Enabled = true,
                        Text = true,
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(providers);

        Assert.NotNull(active);
        Assert.Equal("enabled-capable", active!.Model.Id);
    }

    [Fact]
    public void TextSelectionPrefersExplicitActiveTextModel()
    {
        var providers = new List<AiProvider>
        {
            new()
            {
                Id = "first",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "fallback",
                        Enabled = true,
                        Text = true,
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            },
            new()
            {
                Id = "second",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "active",
                        Enabled = true,
                        Text = true,
                        ActiveText = true,
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(providers);

        Assert.NotNull(active);
        Assert.Equal("second", active!.Provider.Id);
        Assert.Equal("active", active.Model.Id);
    }

    [Fact]
    public void TextSelectionFallsBackWhenExplicitActiveModelIsUnavailable()
    {
        var providers = new List<AiProvider>
        {
            new()
            {
                Id = "first",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "disabled-active",
                        Enabled = false,
                        Text = true,
                        ActiveText = true,
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    },
                    new()
                    {
                        Id = "fallback",
                        Enabled = true,
                        Text = true,
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(providers);

        Assert.NotNull(active);
        Assert.Equal("fallback", active!.Model.Id);
    }

    [Fact]
    public void ImageSelectionRequiresUserImageEnablementAndImageCapability()
    {
        var service = new ImageGenerationService(null!, null!, new NoOpCapabilityCatalog());
        var providers = new List<AiProvider>
        {
            new()
            {
                Id = "provider",
                Name = "Provider",
                Type = "openai",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "capable-not-enabled",
                        Enabled = true,
                        Image = false,
                        Capabilities = new() { TextInput = true, ImageOutput = true }
                    },
                    new()
                    {
                        Id = "enabled-not-capable",
                        Enabled = true,
                        Image = true,
                        Capabilities = new() { TextInput = true, ImageOutput = false }
                    },
                    new()
                    {
                        Id = "enabled-capable",
                        Enabled = true,
                        Image = true,
                        Capabilities = new() { TextInput = true, ImageOutput = true }
                    }
                ]
            }
        };

        var models = service.GetEnabledImageModels(providers);

        Assert.Single(models);
        Assert.Equal("enabled-capable", models[0].ModelId);
    }

    [Fact]
    public async Task BulkModelToggleSelectsOnlyChatModels()
    {
        await using var store = new LiveRoleplayStore(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        var provider = new AiProvider
        {
            Id = "provider",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "chat",
                    Capabilities = new() { TextInput = true, TextOutput = true }
                },
                new()
                {
                    Id = "image",
                    Capabilities = new() { TextInput = true, TextOutput = false, ImageOutput = true }
                },
                new()
                {
                    Id = "unsupported",
                    Capabilities = new() { TextInput = false, TextOutput = false, ImageOutput = false }
                }
            ]
        };
        await session.Providers.AddAsync(provider);

        await session.Providers.SetModelsAsync(provider, true);

        Assert.True(provider.Models[0].Enabled);
        Assert.True(provider.Models[0].Text);
        Assert.False(provider.Models[0].Image);
        Assert.False(provider.Models[1].Enabled);
        Assert.False(provider.Models[1].Text);
        Assert.False(provider.Models[1].Image);
        Assert.False(provider.Models[2].Enabled);
        Assert.False(provider.Models[2].Text);
        Assert.False(provider.Models[2].Image);

        await session.Providers.SetModelsAsync(provider, false);

        Assert.All(provider.Models, model =>
        {
            Assert.False(model.Enabled);
            Assert.False(model.Text);
            Assert.False(model.Image);
        });
    }

    [Fact]
    public void ModelSelectionRulesKeepChatAndImageSelectionSeparate()
    {
        var model = new AiProviderModel
        {
            Capabilities = new() { TextInput = true, TextOutput = true, ImageOutput = true }
        };

        AiProviderModelSelectionRules.SetImageSelected(model, true);

        Assert.True(AiProviderModelSelectionRules.IsSelectedForImage(model));
        Assert.False(AiProviderModelSelectionRules.IsSelectedForChat(model));

        AiProviderModelSelectionRules.SetChatSelected(model, true);

        Assert.True(AiProviderModelSelectionRules.IsSelectedForChat(model));
        Assert.True(AiProviderModelSelectionRules.IsSelectedForImage(model));

        AiProviderModelSelectionRules.SetChatSelected(model, false);

        Assert.True(model.Enabled);
        Assert.False(AiProviderModelSelectionRules.IsSelectedForChat(model));
        Assert.True(AiProviderModelSelectionRules.IsSelectedForImage(model));
    }

    [Fact]
    public async Task ProviderStorePersistsOneGlobalActiveTextModel()
    {
        await using var store = new LiveRoleplayStore(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        var provider = new AiProvider
        {
            Id = "active-provider",
            Name = "Active Provider",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "first",
                    Enabled = true,
                    Text = true,
                    ActiveText = true,
                    Capabilities = new() { TextInput = true, TextOutput = true }
                },
                new()
                {
                    Id = "second",
                    Enabled = true,
                    Text = true,
                    Capabilities = new() { TextInput = true, TextOutput = true }
                }
            ]
        };
        await session.Providers.AddAsync(provider);

        await session.Providers.SetActiveTextModelAsync("active-provider", "second");

        var reloaded = new RoleplaySession(store);
        await reloaded.InitializeAsync();
        var activeModels = reloaded.Providers.Items.SelectMany(item => item.Models).Where(model => model.ActiveText).ToList();
        Assert.Single(activeModels);
        Assert.Equal("second", activeModels[0].Id);
    }

    [Fact]
    public void HuggingFaceEndpointNormalizerMatchesRawAndResponsesUrls()
    {
        var raw = AgentEndpointUrlNormalizer.Normalize("https://abc.us-east-1.aws.endpoints.huggingface.cloud");
        var responses = AgentEndpointUrlNormalizer.Normalize("https://abc.us-east-1.aws.endpoints.huggingface.cloud/v1/");

        Assert.Equal(raw, responses);
        Assert.Equal("https://abc.us-east-1.aws.endpoints.huggingface.cloud/v1/", AgentEndpointUrlNormalizer.NormalizeResponsesEndpoint("https://abc.us-east-1.aws.endpoints.huggingface.cloud"));
    }

    [Fact]
    public async Task HuggingFaceDiscoveryMapsManagedEndpointsToModels()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["https://huggingface.co/api/whoami-v2"] = """{"name":"josh","orgs":[{"name":"team"}]}""",
            ["https://api.endpoints.huggingface.cloud/v2/endpoint/josh?limit=100"] = """
                {
                  "items": [
                    {
                      "name": "rp-endpoint",
                      "model": { "repository": "org/rp-model" },
                      "status": { "url": "https://rp.us-east-1.aws.endpoints.huggingface.cloud" }
                    }
                  ]
                }
                """,
            ["https://api.endpoints.huggingface.cloud/v2/endpoint/team?limit=100"] = """{"items":[]}"""
        };
        var service = new AiProviderConnectionService(new FakeHttpClientFactory(responses), new NoOpCapabilityCatalog());

        var models = await service.DiscoverModelsAsync(new()
        {
            Type = "huggingface",
            Name = "Hugging Face",
            ApiKey = "hf-token"
        });

        var model = Assert.Single(models);
        Assert.Equal("org/rp-model", model.Id);
        Assert.Equal("rp-endpoint (org/rp-model)", model.DisplayName);
        Assert.Equal("https://rp.us-east-1.aws.endpoints.huggingface.cloud", model.Endpoint);
        Assert.Equal("org/rp-model", model.Repository);
    }

    sealed class NoOpCapabilityCatalog : IModelCapabilityCatalog
    {
        public string UserCatalogPath => "";

        public ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model) => model.Capabilities;

        public ModelGenerationCapabilities Resolve(string providerType, string modelId) => ModelGenerationCapabilities.Fallback;

        public void ApplyResolvedCapabilities(AiProvider provider)
        {
        }

        public void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities)
        {
        }

        public void UpdateLiveGrokCapabilities(JsonNode languageModelsJson)
        {
        }
    }

    sealed class FakeHttpClientFactory(IReadOnlyDictionary<string, string> responses) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(responses));
    }

    sealed class Handler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null && responses.TryGetValue(request.RequestUri.AbsoluteUri, out var body))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        }
    }
}

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
                        Roles = [],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    },
                    new()
                    {
                        Id = "enabled-not-capable",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = false, TextOutput = false }
                    },
                    new()
                    {
                        Id = "enabled-capable",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
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
                        Roles = [AiModelRole.Chat],
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
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };
        var selections = new ActiveModelSelectionsState
        {
            Values =
            {
                [AiModelRole.Chat] = new() { ProviderId = "second", ModelId = "active" }
            }
        };

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(providers, selections);

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
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    },
                    new()
                    {
                        Id = "fallback",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };
        var selections = new ActiveModelSelectionsState
        {
            Values =
            {
                [AiModelRole.Chat] = new() { ProviderId = "first", ModelId = "disabled-active" }
            }
        };

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(providers, selections);

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
                        Roles = [],
                        Capabilities = new() { TextInput = true, ImageOutput = true }
                    },
                    new()
                    {
                        Id = "enabled-not-capable",
                        Enabled = true,
                        Roles = [AiModelRole.Image],
                        Capabilities = new() { TextInput = true, ImageOutput = false }
                    },
                    new()
                    {
                        Id = "enabled-capable",
                        Enabled = true,
                        Roles = [AiModelRole.Image],
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
    public async Task BulkModelToggleSelectsEveryAvailableRole()
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
        Assert.True(AiProviderModelSelectionRules.IsSelectedForChat(provider.Models[0]));
        Assert.False(AiProviderModelSelectionRules.IsSelectedForImage(provider.Models[0]));
        Assert.True(provider.Models[1].Enabled);
        Assert.False(AiProviderModelSelectionRules.IsSelectedForChat(provider.Models[1]));
        Assert.True(AiProviderModelSelectionRules.IsSelectedForImage(provider.Models[1]));
        Assert.False(provider.Models[2].Enabled);
        Assert.False(AiProviderModelSelectionRules.IsSelectedForChat(provider.Models[2]));
        Assert.False(AiProviderModelSelectionRules.IsSelectedForImage(provider.Models[2]));

        await session.Providers.SetModelsAsync(provider, false);

        Assert.All(provider.Models, model =>
        {
            Assert.False(model.Enabled);
            Assert.Empty(model.Roles);
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
    public void ModelSelectionRulesCanSelectEveryAvailableRoleAfterSetup()
    {
        var model = new AiProviderModel
        {
            Capabilities = new() { TextInput = true, TextOutput = true, ImageOutput = true }
        };

        AiProviderModelSelectionRules.SelectAvailableRoles(model);

        Assert.True(model.Enabled);
        Assert.True(AiProviderModelSelectionRules.IsSelectedForChat(model));
        Assert.True(AiProviderModelSelectionRules.IsSelectedForImage(model));
    }

    [Fact]
    public void ModelSelectionRulesDoNotSelectUnsupportedRolesAfterSetup()
    {
        var model = new AiProviderModel
        {
            Enabled = true,
            Roles = [AiModelRole.Chat, AiModelRole.Image],
            Capabilities = new() { TextInput = false, TextOutput = false, ImageOutput = false }
        };

        AiProviderModelSelectionRules.SelectAvailableRoles(model);

        Assert.False(model.Enabled);
        Assert.Empty(model.Roles);
    }

    [Fact]
    public async Task ChatModelSelectionPersistsActiveTextModel()
    {
        await using var store = new LiveRoleplayStore(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var notifier = new RecordingModelSelectionNotifier();
        var globalSelections = new GlobalModelSelectionStore(new InMemoryAppSettingsService(), notifier);
        var session = new RoleplaySession(store, globalModelSelectionStore: globalSelections, modelSelectionNotifier: notifier);
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
                    Roles = [AiModelRole.Chat],
                    Capabilities = new() { TextInput = true, TextOutput = true }
                },
                new()
                {
                    Id = "second",
                    Enabled = true,
                    Roles = [AiModelRole.Chat],
                    Capabilities = new() { TextInput = true, TextOutput = true }
                }
            ]
        };
        await session.Providers.AddAsync(provider);

        await session.ModelSelection.SetActiveModelAsync(AiModelRole.Chat, "active-provider", "second");

        var reloaded = new RoleplaySession(store, globalModelSelectionStore: globalSelections, modelSelectionNotifier: notifier);
        await reloaded.InitializeAsync();
        var active = reloaded.ModelSelection.Resolve(AiModelRole.Chat);
        Assert.Equal("second", active?.Model.Id);
    }

    [Fact]
    public async Task GlobalSelectionSavesFallbackWhenSelectionIsMissing()
    {
        var selections = new GlobalModelSelectionStore(new InMemoryAppSettingsService(), new RecordingModelSelectionNotifier());
        var providers = new List<AiProvider>
        {
            new()
            {
                Id = "provider",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "fallback",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };

        await selections.EnsureValidAsync(providers);

        var snapshot = selections.Snapshot();
        Assert.Equal("provider", snapshot.Values[AiModelRole.Chat].ProviderId);
        Assert.Equal("fallback", snapshot.Values[AiModelRole.Chat].ModelId);
    }

    [Fact]
    public async Task GlobalSelectionFallsBackAndPersistsWhenActiveModelBecomesInvalid()
    {
        var selections = new GlobalModelSelectionStore(new InMemoryAppSettingsService(), new RecordingModelSelectionNotifier());
        var providers = new List<AiProvider>
        {
            new()
            {
                Id = "provider",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "active",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    },
                    new()
                    {
                        Id = "fallback",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };
        await selections.SetActiveModelAsync(AiModelRole.Chat, "provider", "active", providers);
        providers[0].Models[0].Enabled = false;

        await selections.EnsureValidAsync(providers);

        Assert.Equal("fallback", selections.Snapshot().Values[AiModelRole.Chat].ModelId);
    }

    [Fact]
    public async Task ReasoningSelectionFallsBackToChatCapableModel()
    {
        var selections = new GlobalModelSelectionStore(new InMemoryAppSettingsService(), new RecordingModelSelectionNotifier());
        var providers = new List<AiProvider>
        {
            new()
            {
                Id = "provider",
                Enabled = true,
                Models =
                [
                    new()
                    {
                        Id = "chat-model",
                        Enabled = true,
                        Roles = [AiModelRole.Chat],
                        Capabilities = new() { TextInput = true, TextOutput = true }
                    }
                ]
            }
        };

        await selections.EnsureValidAsync(providers);

        var active = selections.Resolve(AiModelRole.Reasoning, providers);
        Assert.Equal("chat-model", active?.Model.Id);
        Assert.Equal("chat-model", selections.Snapshot().Values[AiModelRole.Reasoning].ModelId);
    }

    [Fact]
    public async Task GlobalSelectionLeavesRoleUnresolvedWhenNoValidModelExists()
    {
        var selections = new GlobalModelSelectionStore(new InMemoryAppSettingsService(), new RecordingModelSelectionNotifier());

        await selections.EnsureValidAsync([]);

        Assert.Null(selections.Resolve(AiModelRole.Chat, []));
        Assert.False(selections.Snapshot().Values.ContainsKey(AiModelRole.Chat));
    }

    [Fact]
    public async Task ModelSelectionNotifierUpdatesOtherSessions()
    {
        await using var store = new LiveRoleplayStore(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var notifier = new RecordingModelSelectionNotifier();
        var globalSelections = new GlobalModelSelectionStore(new InMemoryAppSettingsService(), notifier);
        var sessionA = new RoleplaySession(store, globalModelSelectionStore: globalSelections, modelSelectionNotifier: notifier);
        var sessionB = new RoleplaySession(store, globalModelSelectionStore: globalSelections, modelSelectionNotifier: notifier);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        var notifications = 0;
        sessionB.ModelSelection.SelectionChanged += notification =>
        {
            if (notification.Role == AiModelRole.Chat)
                notifications++;

            return Task.CompletedTask;
        };
        var provider = new AiProvider
        {
            Id = "active-provider",
            Name = "Active Provider",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "selected",
                    Enabled = true,
                    Roles = [AiModelRole.Chat],
                    Capabilities = new() { TextInput = true, TextOutput = true }
                }
            ]
        };
        await sessionA.Providers.AddAsync(provider);

        await sessionA.ModelSelection.SetActiveModelAsync(AiModelRole.Chat, "active-provider", "selected");

        Assert.True(notifications > 0);
        Assert.Equal("selected", sessionB.ModelSelection.Resolve(AiModelRole.Chat)?.Model.Id);
    }

    [Fact]
    public void CapabilityPipelinePreservesElevenLabsVoiceSelectionAfterResolvingCapabilities()
    {
        var catalog = new FixedCapabilityCatalog(new()
        {
            [("elevenlabs", "eleven_multilingual_v2")] = new() { TextInput = true, TextOutput = false, SpeechOutput = true }
        });
        var pipeline = new AiProviderCapabilityPipeline(catalog);
        var provider = new AiProvider
        {
            Type = "elevenlabs",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "eleven_multilingual_v2",
                    Enabled = true,
                    Roles = [AiModelRole.Voice]
                }
            ]
        };

        pipeline.Normalize(provider);

        Assert.True(AiProviderModelSelectionRules.IsSelectedForVoice(provider.Models[0]));
    }

    [Fact]
    public void CapabilityPipelineRemovesUnsupportedRolesAfterCapabilitiesResolve()
    {
        var catalog = new FixedCapabilityCatalog(new()
        {
            [("compatible", "text-only")] = new() { TextInput = true, TextOutput = true, SpeechOutput = false }
        });
        var pipeline = new AiProviderCapabilityPipeline(catalog);
        var provider = new AiProvider
        {
            Type = "compatible",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "text-only",
                    Enabled = true,
                    Roles = [AiModelRole.Voice]
                }
            ]
        };

        pipeline.Normalize(provider);

        Assert.False(provider.Models[0].Enabled);
        Assert.Empty(provider.Models[0].Roles);
    }

    [Fact]
    public void CapabilityPipelineAddsAndSelectsProviderManagedXAiTtsModel()
    {
        var catalog = new FixedCapabilityCatalog(new()
        {
            [("grok", AiProviderModelIdentityRules.XAiTextToSpeechModelId)] = new() { TextInput = true, TextOutput = false, SpeechOutput = true }
        });
        var pipeline = new AiProviderCapabilityPipeline(catalog);
        var provider = new AiProvider
        {
            Type = "grok",
            ApiKey = "xai-key",
            Enabled = true
        };

        pipeline.Normalize(provider);

        var model = Assert.Single(provider.Models);
        Assert.Equal(AiProviderModelIdentityRules.XAiTextToSpeechModelId, model.Id);
        Assert.True(AiProviderModelSelectionRules.IsSelectedForVoice(model));
    }

    [Fact]
    public async Task ActiveModelResolutionDoesNotMutateProviderRoles()
    {
        await using var store = new LiveRoleplayStore(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var session = new RoleplaySession(
            store,
            new NoOpCapabilityCatalog(),
            capabilityPipeline: new NoOpCapabilityPipeline());
        await session.InitializeAsync();
        var provider = new AiProvider
        {
            Id = "provider",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "voice-with-unresolved-capability",
                    Enabled = true,
                    Roles = [AiModelRole.Voice],
                    Capabilities = ModelGenerationCapabilities.Fallback
                }
            ]
        };
        await session.Providers.AddAsync(provider);

        var active = session.ModelSelection.Resolve(AiModelRole.Voice);

        Assert.Null(active);
        Assert.Contains(AiModelRole.Voice, provider.Models[0].Roles);
        Assert.True(provider.Models[0].Enabled);
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

    sealed class FixedCapabilityCatalog(Dictionary<(string Provider, string Model), ModelGenerationCapabilities> capabilities) : IModelCapabilityCatalog
    {
        public string UserCatalogPath => "";

        public ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model) => Resolve(provider.Type, model.Id);

        public ModelGenerationCapabilities Resolve(string providerType, string modelId) =>
            capabilities.TryGetValue((providerType, modelId), out var value) ? value : ModelGenerationCapabilities.Fallback;

        public void ApplyResolvedCapabilities(AiProvider provider)
        {
            foreach (var model in provider.Models)
                model.Capabilities = Resolve(provider, model);
        }

        public void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities)
        {
        }

        public void UpdateLiveGrokCapabilities(JsonNode languageModelsJson)
        {
        }
    }

    sealed class NoOpCapabilityPipeline : IAiProviderCapabilityPipeline
    {
        public void Normalize(AiProvider provider)
        {
        }

        public void Normalize(IEnumerable<AiProvider> providers)
        {
        }
    }

    sealed class RecordingModelSelectionNotifier : IModelSelectionNotifier
    {
        public List<ModelSelectionChangeNotification> Notifications { get; } = [];
        public event Func<ModelSelectionChangeNotification, Task>? Changed;

        public async Task PublishAsync(ModelSelectionChangeNotification notification)
        {
            Notifications.Add(notification);
            var changed = Changed;
            if (changed is not null)
                await changed(notification);
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

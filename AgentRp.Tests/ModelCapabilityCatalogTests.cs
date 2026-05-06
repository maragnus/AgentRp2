using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace AgentRp.Tests;

public sealed class ModelCapabilityCatalogTests
{
    [Fact]
    public void ResolvesCapabilitiesByUserLiveDefaultFallbackPriority()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """
            {
              "models": [
                {
                  "provider": "grok",
                  "id": "shared-model",
                  "textInput": true,
                  "textOutput": true,
                  "structuredOutput": false,
                  "temperature": "Supported"
                },
                {
                  "provider": "openai",
                  "id": "default-only",
                  "textInput": true,
                  "textOutput": true,
                  "structuredOutput": true
                }
              ]
            }
            """);
        var userPath = Path.Combine(root, "model-capabilities.user.json");
        File.WriteAllText(userPath, """
            {
              "models": [
                {
                  "provider": "grok",
                  "id": "shared-model",
                  "textInput": true,
                  "textOutput": true,
                  "imageOutput": true,
                  "imageGenerationModel": "gpt-image-test",
                  "structuredOutput": true,
                  "temperature": "DefaultOnly"
                }
              ]
            }
            """);

        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), userPath);
        catalog.UpdateLiveGrokCapabilities(JsonNode.Parse("""
            {
              "models": [
                {
                  "id": "shared-model",
                  "input_modalities": [ "text", "image" ],
                  "output_modalities": [ "text" ],
                  "aliases": [ "shared-alias" ]
                }
              ]
            }
            """)!);

        var live = catalog.Resolve("grok", "shared-alias");
        var fromDefault = catalog.Resolve("openai", "default-only");
        var fallback = catalog.Resolve("compatible", "unknown-model");

        Assert.Equal("user", live.Source);
        Assert.True(live.ImageInput);
        Assert.True(live.StructuredOutput);
        Assert.True(live.Tools);
        Assert.True(live.ImageOutput);
        Assert.Equal("gpt-image-test", live.ImageGenerationModel);
        Assert.Equal(TuningSupport.DefaultOnly, live.Temperature);
        Assert.Equal("default", fromDefault.Source);
        Assert.True(fromDefault.StructuredOutput);
        Assert.True(fromDefault.Tools);
        Assert.Equal("fallback", fallback.Source);
        Assert.True(fallback.CanGenerateText);
        Assert.True(fallback.StructuredOutput);
        Assert.True(fallback.Streaming);
        Assert.False(fallback.Tools);
        Assert.False(fallback.ImageOutput);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("claude")]
    [InlineData("grok")]
    public void KnownResponsesProvidersDefaultToToolSupport(string providerType)
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """{ "models": [] }""");
        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), Path.Combine(root, "model-capabilities.user.json"));

        var capabilities = catalog.Resolve(providerType, "unknown-model");

        Assert.True(capabilities.Tools);
    }

    [Fact]
    public void ExplicitToolDisableOverridesProviderDefault()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """
            {
              "models": [
                {
                  "provider": "openai",
                  "id": "no-tools-model",
                  "textInput": true,
                  "textOutput": true,
                  "tools": false
                }
              ]
            }
            """);
        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), Path.Combine(root, "model-capabilities.user.json"));

        var capabilities = catalog.Resolve("openai", "no-tools-model");

        Assert.False(capabilities.Tools);
    }

    [Fact]
    public void ApplyingResolvedCapabilitiesDoesNotOverwriteUserRoleSelections()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """
            {
              "models": [
                {
                  "provider": "openai",
                  "id": "chat-and-visual",
                  "textInput": true,
                  "textOutput": true,
                  "imageOutput": true
                }
              ]
            }
            """);
        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), Path.Combine(root, "model-capabilities.user.json"));
        var provider = new AiProvider
        {
            Type = "openai",
            Models =
            [
                new()
                {
                    Id = "chat-and-visual",
                    Enabled = true,
                    Text = false,
                    Image = true
                }
            ]
        };

        catalog.ApplyResolvedCapabilities(provider);

        Assert.False(provider.Models[0].Text);
        Assert.True(provider.Models[0].Image);
        Assert.True(provider.Models[0].Capabilities.CanGenerateText);
        Assert.True(provider.Models[0].Capabilities.CanGenerateImage);
    }

    [Theory]
    [InlineData("gpt-image-1.5")]
    [InlineData("dall-e-3")]
    public void OpenAiImageNamedModelsResolveAsImageGenerationOnly(string modelId)
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """{ "models": [] }""");
        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), Path.Combine(root, "model-capabilities.user.json"));

        var capabilities = catalog.Resolve("openai", modelId);

        Assert.False(capabilities.CanGenerateText);
        Assert.True(capabilities.CanGenerateImage);
        Assert.False(capabilities.Streaming);
        Assert.False(capabilities.StructuredOutput);
    }

    [Fact]
    public void SaveUserCapabilitiesPersistsAndResolvesUserSetup()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """{ "models": [] }""");
        var userPath = Path.Combine(root, "model-capabilities.user.json");
        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), userPath);

        catalog.SaveUserCapabilities("compatible", "custom-ready-model", new ModelGenerationCapabilities
        {
            TextInput = true,
            TextOutput = true,
            ImageInput = true,
            StructuredOutput = true,
            Streaming = true,
            Temperature = TuningSupport.Supported,
            TopP = TuningSupport.DefaultOnly
        });

        var reloaded = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), userPath);
        var capabilities = reloaded.Resolve("compatible", "custom-ready-model");

        Assert.Equal("user", capabilities.Source);
        Assert.True(capabilities.CanGenerateText);
        Assert.True(capabilities.ImageInput);
        Assert.True(capabilities.StructuredOutput);
        Assert.True(capabilities.Streaming);
        Assert.Equal(TuningSupport.Supported, capabilities.Temperature);
        Assert.Equal(TuningSupport.DefaultOnly, capabilities.TopP);
    }

    [Fact]
    public void ExplicitUserDisableOverridesRoleplayReadyDefaults()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(root, "wwwroot", "model-capabilities.default.json"), """{ "models": [] }""");
        var userPath = Path.Combine(root, "model-capabilities.user.json");
        var catalog = new ModelCapabilityCatalog(new FakeWebHostEnvironment(root), userPath);

        catalog.SaveUserCapabilities("compatible", "prose-only-model", new ModelGenerationCapabilities
        {
            TextInput = true,
            TextOutput = true,
            StructuredOutput = false,
            Streaming = false
        });

        var capabilities = catalog.Resolve("compatible", "prose-only-model");

        Assert.True(capabilities.CanGenerateText);
        Assert.False(capabilities.StructuredOutput);
        Assert.False(capabilities.Streaming);
    }

    static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"AgentRpCapabilityTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    sealed class FakeWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AgentRp.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(Path.Combine(root, "wwwroot"));
    }
}

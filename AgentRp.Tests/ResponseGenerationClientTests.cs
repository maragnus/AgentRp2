#pragma warning disable OPENAI001

using System.Reflection;
using AgentRp.Models;
using AgentRp.Services;
using OpenAI.Responses;

namespace AgentRp.Tests;

public sealed class ResponseGenerationClientTests
{
    [Fact]
    public void ImageResponseOptionsEnableStreaming()
    {
        var request = new ResponseImageGenerationRequest(
            new AiProvider { Name = "OpenAI", Type = "openai" },
            new AiProviderModel { Id = "gpt-5.5" },
            new ModelGenerationCapabilities
            {
                TextInput = true,
                TextOutput = true,
                Tools = true
            },
            new AiProviderModel { Id = "gpt-image-1-mini" },
            new ModelGenerationCapabilities
            {
                TextInput = true,
                ImageOutput = true
            },
            "Generate a small test image.",
            "1024x1024",
            "auto",
            "auto",
            [],
            "Generating a test image");

        var options = BuildImageOptions(request);

        Assert.True(options.StreamingEnabled);
        Assert.Equal("gpt-5.5", options.Model);
        var tool = Assert.IsType<ImageGenerationTool>(Assert.Single(options.Tools));
        Assert.Equal(ImageGenerationToolQuality.Auto, tool.Quality);
        Assert.Equal(ImageGenerationToolSize.W1024xH1024, tool.Size);
        Assert.Equal(ImageGenerationToolOutputFileFormat.Png, tool.OutputFileFormat);
        Assert.Equal(2, tool.PartialImageCount);
    }

    [Fact]
    public void ImageResponseOptionsOmitUnsupportedInputFidelity()
    {
        var request = new ResponseImageGenerationRequest(
            new AiProvider { Name = "OpenAI", Type = "openai" },
            new AiProviderModel { Id = "gpt-5.5" },
            new ModelGenerationCapabilities
            {
                TextInput = true,
                TextOutput = true,
                Tools = true
            },
            new AiProviderModel { Id = "gpt-image-2" },
            new ModelGenerationCapabilities
            {
                TextInput = true,
                ImageOutput = true,
                ImageInputFidelity = false
            },
            "Generate a small test image.",
            "1024x1024",
            "auto",
            "high",
            [],
            "Generating a test image");

        var fidelity = InputFidelityFor(request);

        Assert.Null(fidelity);
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("high", "high")]
    [InlineData("auto", "auto")]
    [InlineData("medium", "auto")]
    public void ImageResponseOptionsMapReferenceDetailToInputImageDetail(string value, string expected)
    {
        var detail = ReferenceImageDetailFor(value);

        Assert.Equal(expected, detail.ToString());
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("medium", "medium")]
    [InlineData("high", "high")]
    [InlineData("auto", "auto")]
    public void ImageResponseOptionsMapQuality(string value, string expected)
    {
        var quality = QualityFor(value);

        Assert.Equal(expected, quality.ToString());
    }

    static CreateResponseOptions BuildImageOptions(ResponseImageGenerationRequest request)
    {
        var method = typeof(OpenAiModelGenerationClient).GetMethod("BuildImageOptions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find image response option builder.");
        return (CreateResponseOptions)method.Invoke(null, [request])!;
    }

    static ImageGenerationToolInputFidelity? InputFidelityFor(ResponseImageGenerationRequest request)
    {
        var method = typeof(OpenAiModelGenerationClient).GetMethod("InputFidelityFor", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find image input fidelity builder.");
        return (ImageGenerationToolInputFidelity?)method.Invoke(null, [request]);
    }

    static ResponseImageDetailLevel ReferenceImageDetailFor(string value)
    {
        var method = typeof(OpenAiModelGenerationClient).GetMethod("ReferenceImageDetailFor", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find image detail builder.");
        return (ResponseImageDetailLevel)method.Invoke(null, [value])!;
    }

    static ImageGenerationToolQuality QualityFor(string value)
    {
        var method = typeof(OpenAiModelGenerationClient).GetMethod("QualityFor", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find image quality builder.");
        return (ImageGenerationToolQuality)method.Invoke(null, [value])!;
    }
}

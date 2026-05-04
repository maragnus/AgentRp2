using System.Text.Json.Nodes;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class TextModelTuningCatalogTests
{
    [Fact]
    public void DefaultModelTuningLeavesTemperatureUnset()
    {
        var defaults = ModelTuningState.CreateDefault();

        Assert.All(defaults.Values.Values, step => Assert.Null(step.Temperature));
    }

    [Fact]
    public void Gpt55TemperatureIsOmittedWhenCustomValueIsUnsupported()
    {
        var body = new JsonObject();

        TextModelTuningCatalog.Apply(body, "openai", "gpt-5.5", new ModelTuningStepState
        {
            Temperature = 0.4
        });

        Assert.False(body.ContainsKey("temperature"));
    }

    [Fact]
    public void LegacyTemperatureIsStillSentForSupportedModels()
    {
        var body = new JsonObject();

        TextModelTuningCatalog.Apply(body, "openai", "gpt-4o", new ModelTuningStepState
        {
            Temperature = 0.4
        });

        Assert.Equal(0.4, body["temperature"]!.GetValue<double>());
    }
}

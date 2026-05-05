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
        var tuning = TextModelTuningCatalog.Filter(new ModelTuningStepState
        {
            Temperature = 0.4
        }, new ModelGenerationCapabilities { Temperature = TuningSupport.DefaultOnly });

        Assert.Null(tuning.Temperature);
    }

    [Fact]
    public void LegacyTemperatureIsStillSentForSupportedModels()
    {
        var tuning = TextModelTuningCatalog.Filter(new ModelTuningStepState
        {
            Temperature = 0.4
        }, new ModelGenerationCapabilities { Temperature = TuningSupport.Supported });

        Assert.Equal(0.4f, tuning.Temperature);
    }
}

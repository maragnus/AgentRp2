using AgentRp.Components.Common;
using AgentRp.Components.StoryCards;
using AgentRp.Models;
using AgentRp.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class StoryCardRequirementPickerTests
{
    [Fact]
    public async Task CreateButtonUpdatesInsideOpenPopoverWhenDraftChanges()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        StoryCardNewRequirementRequest? added = null;
        var picker = context.Render<StoryCardRequirementPicker>(parameters => parameters
            .Add(item => item.PhaseId, "phase")
            .Add(item => item.ChildCardType, StoryCardChildCardType.Role)
            .Add(item => item.Icon, "user")
            .Add(item => item.OnAddNewRequirement, request =>
            {
                added = request;
                return Task.CompletedTask;
            }));
        var overlays = context.Render<OverlayHost>();

        picker.Find("button").Click();
        var createButton = overlays.Find("button[title='Create role card']");
        Assert.True(createButton.HasAttribute("disabled"));

        var input = overlays.FindComponent<AppInput>();
        await input.InvokeAsync(() => input.Instance.NotifyTextValueChanged("  Mentor  "));

        createButton = overlays.Find("button[title='Create role card']");
        Assert.False(createButton.HasAttribute("disabled"));

        createButton.Click();

        Assert.NotNull(added);
        Assert.Equal("phase", added.PhaseId);
        Assert.Equal(StoryCardChildCardType.Role, added.ChildCardType);
        Assert.Equal("Mentor", added.Title);
    }
}

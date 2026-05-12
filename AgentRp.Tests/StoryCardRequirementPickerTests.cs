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
    public void CreateFormSubmitsCurrentCommandTextInsideOpenPopover()
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
        var input = overlays.Find("[data-text-command-input]");
        var groupId = input.GetAttribute("data-text-command-group") ?? "";
        Assert.True(createButton.HasAttribute("disabled"));
        Assert.Equal(groupId, createButton.GetAttribute("data-text-command-group"));
        context.JSInterop.Setup<string>("agentRp.textCommands.value", groupId)
            .SetResult("  Mentor  ");

        overlays.Find("form.story-card-requirement-create").Submit();

        Assert.NotNull(added);
        Assert.Equal("phase", added.PhaseId);
        Assert.Equal(StoryCardChildCardType.Role, added.ChildCardType);
        Assert.Equal("Mentor", added.Title);
    }
}

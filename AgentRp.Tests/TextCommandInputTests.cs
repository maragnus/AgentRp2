using AgentRp.Components.Chat;
using AgentRp.Components.Entities;
using AgentRp.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class TextCommandInputTests
{
    [Fact]
    public void TranscriptComposerRendersCommandContract()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddScoped<OverlayService>();

        var component = context.Render<TranscriptComposer>(parameters => parameters
            .Add(item => item.CanGenerate, true));
        var input = component.Find("[data-text-command-input]");
        var groupId = input.GetAttribute("data-text-command-group");

        Assert.False(string.IsNullOrWhiteSpace(groupId));
        Assert.Equal("manual", input.GetAttribute("data-text-command-enter"));
        Assert.Equal("generate", input.GetAttribute("data-text-command-ctrl-enter"));
        Assert.Equal(groupId, component.Find("[data-text-command-action='manual']").GetAttribute("data-text-command-group"));
        Assert.Equal("true", component.Find("[data-text-command-action='manual']").GetAttribute("data-text-command-requires-text"));
        Assert.Equal("false", component.Find("[data-text-command-action='generate']").GetAttribute("data-text-command-requires-text"));
    }

    [Fact]
    public void TranscriptComposerSubmitsCurrentJsText()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddScoped<OverlayService>();
        var posted = "";

        var component = context.Render<TranscriptComposer>(parameters => parameters
            .Add(item => item.CanGenerate, true)
            .Add(item => item.OnPostManual, text =>
            {
                posted = text;
                return Task.CompletedTask;
            }));
        var groupId = component.Find("[data-text-command-input]").GetAttribute("data-text-command-group") ?? "";
        context.JSInterop.Setup<string>("agentRp.textCommands.value", groupId)
            .SetResult("  A direct transcript post.  ");

        var button = component.Find("[data-text-command-action='manual']");
        button.RemoveAttribute("disabled");
        button.Click();

        Assert.Equal("  A direct transcript post.  ", posted);
    }

    [Fact]
    public void StoryAssistantComposerSubmitsCurrentJsText()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var sent = "";

        var component = context.Render<StoryAssistantComposer>(parameters => parameters
            .Add(item => item.CanRun, true)
            .Add(item => item.OnSend, text =>
            {
                sent = text;
                return Task.CompletedTask;
            }));
        var groupId = component.Find("[data-text-command-input]").GetAttribute("data-text-command-group") ?? "";
        context.JSInterop.Setup<string>("agentRp.textCommands.value", groupId)
            .SetResult("  Revise this character.  ");

        var button = component.Find("[data-text-command-action='send']");
        button.RemoveAttribute("disabled");
        button.Click();

        Assert.Equal("  Revise this character.  ", sent);
    }
}

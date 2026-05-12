using AgentRp.Components.Chat;
using AgentRp.Components.Common;
using AgentRp.Models;
using AgentRp.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class AppTextInputBaseTests
{
    [Fact]
    public void NoneModeNativeChangeUpdatesValueAndEmptyState()
    {
        using var context = new BunitContext();
        var value = "";
        var isEmpty = true;

        var component = context.Render<AppTextarea>(parameters => parameters
            .Add(item => item.Value, value)
            .Add(item => item.ValueChanged, next =>
            {
                value = next;
                return Task.CompletedTask;
            })
            .Add(item => item.IsEmpty, isEmpty)
            .Add(item => item.IsEmptyChanged, next =>
            {
                isEmpty = next;
                return Task.CompletedTask;
            }));

        component.Find("textarea").Change("Committed text");

        Assert.Equal("Committed text", value);
        Assert.False(isEmpty);
    }

    [Fact]
    public async Task EmptyModeValueNotificationUpdatesNonEmptyTextWithoutEmptyTransition()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var value = "Original";
        var isEmpty = false;
        var emptyChangeCount = 0;

        var component = context.Render<AppTextarea>(parameters => parameters
            .Add(item => item.Value, value)
            .Add(item => item.ValueChanged, next =>
            {
                value = next;
                return Task.CompletedTask;
            })
            .Add(item => item.IsEmpty, isEmpty)
            .Add(item => item.IsEmptyChanged, next =>
            {
                isEmpty = next;
                emptyChangeCount++;
                return Task.CompletedTask;
            })
            .Add(item => item.UpdateMode, TextUpdateMode.Empty));

        await component.InvokeAsync(() => component.Instance.NotifyTextValueChanged("Edited while still non-empty"));

        Assert.Equal("Edited while still non-empty", value);
        Assert.False(isEmpty);
        Assert.Equal(0, emptyChangeCount);
    }

    [Theory]
    [InlineData(TextUpdateMode.Change)]
    [InlineData(TextUpdateMode.Live)]
    public async Task ValueNotificationUpdatesEmptyStateInTrackedModes(TextUpdateMode mode)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var value = "";
        var isEmpty = true;

        var component = context.Render<AppInput>(parameters => parameters
            .Add(item => item.Value, value)
            .Add(item => item.ValueChanged, next =>
            {
                value = next;
                return Task.CompletedTask;
            })
            .Add(item => item.IsEmpty, isEmpty)
            .Add(item => item.IsEmptyChanged, next =>
            {
                isEmpty = next;
                return Task.CompletedTask;
            })
            .Add(item => item.UpdateMode, mode));

        await component.InvokeAsync(() => component.Instance.NotifyTextValueChanged("Typed text"));

        Assert.Equal("Typed text", value);
        Assert.False(isEmpty);

        await component.InvokeAsync(() => component.Instance.NotifyTextValueChanged("   "));

        Assert.Equal("   ", value);
        Assert.True(isEmpty);
    }

    [Fact]
    public async Task CyoaCustomGuidanceSubmitsCurrentFocusedText()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        var submitted = "";
        var decision = new RpCyoaPendingDecision
        {
            Id = "decision",
            Mode = RpCyoaModes.Director,
            RequestedNarrator = true,
            Options =
            [
                new()
                {
                    Id = "continue",
                    Direction = RpCyoaDirections.Continue,
                    Title = "Continue",
                    Summary = "Continue the scene."
                }
            ]
        };

        var component = context.Render<CyoaDecisionPanel>(parameters => parameters
            .Add(item => item.Decision, decision)
            .Add(item => item.OnCustomGuidance, value =>
            {
                submitted = value;
                return Task.CompletedTask;
            }));

        var commandInput = component.Find("[data-text-command-input]");
        var groupId = commandInput.GetAttribute("data-text-command-group") ?? "";
        context.JSInterop.Setup<string>("agentRp.textCommands.value", groupId)
            .SetResult("  Use this exact guidance.  ");

        var button = component.FindAll("button")
            .Single(button => button.TextContent.Contains("Use Guidance", StringComparison.Ordinal));
        button.RemoveAttribute("disabled");
        button.Click();

        Assert.Equal("Use this exact guidance.", submitted);
    }
}

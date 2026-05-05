using AgentRp.Components.Chat;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task TwoSessionsShareOneLoadedLiveChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);

        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.Equal(
            sessionA.Chat.Characters.Items.First(character => character.Id == "c1").InScene,
            sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene);
    }

    [Fact]
    public async Task SelectAsyncAwaitsFullChatContextBeforeSwitchCompletes()
    {
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(liveStore);
        await session.InitializeAsync();

        await session.Chats.SelectAsync("ch2");

        Assert.Equal("ch2", session.Chats.Active?.Id);
        Assert.NotEmpty(session.Chat.Characters.Items);
        Assert.NotEmpty(session.Chat.Locations.Items);
        Assert.NotEmpty(session.Chat.Images.Items);
        Assert.NotEmpty(session.Chat.Transcript.Items);
    }

    [Fact]
    public async Task CharacterChangeInOneSessionUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var notifications = 0;
        sessionB.Chat.Characters.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        var original = sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene;
        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.True(notifications > 0);
        Assert.NotEqual(original, sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene);
    }

    [Fact]
    public async Task TranscriptPostInOneSessionUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Shared live transcript message.", null);

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Shared live transcript message.");
    }

    [Fact]
    public async Task SessionOnDifferentChatIgnoresChatLocalNotification()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        await sessionB.Chats.SelectAsync("ch2");

        var notifications = 0;
        sessionB.Chat.Characters.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task ActiveChatSwitchUsesLiveMemoryInsteadOfFreshSeedClone()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Memory should win.", null);
        await sessionB.Chats.SelectAsync("ch2");
        await sessionB.Chats.SelectAsync("ch1");

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Memory should win.");
    }

    [Fact]
    public async Task ProviderChangesNotifyAllSessions()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var notifications = 0;
        sessionB.Providers.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        var provider = sessionA.Providers.Items.First();
        provider.Enabled = !provider.Enabled;
        await sessionA.Providers.MarkChangedAsync();

        Assert.True(notifications > 0);
        Assert.Equal(provider.Enabled, sessionB.Providers.Items.First(item => item.Id == provider.Id).Enabled);
    }

    [Fact]
    public async Task UnreferencedInactiveChatsCanUnloadAfterTtl()
    {
        await using var liveStore = NewLiveStore(TimeSpan.FromMilliseconds(10));
        var session = new RoleplaySession(liveStore);
        await session.InitializeAsync();

        Assert.True(liveStore.IsChatLoaded("ch1"));

        await session.Chats.SelectAsync("ch2");
        await Task.Delay(20);
        liveStore.CleanupExpiredChats();

        Assert.False(liveStore.IsChatLoaded("ch1"));
        Assert.True(liveStore.IsChatLoaded("ch2"));
    }

    [Fact]
    public async Task ChatAreaRerendersFromCrossSessionTranscriptNotification()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(sessionB));

        await sessionA.Chat.Transcript.PostManualAsync("Rendered from another session.", null);

        component.WaitForAssertion(() => Assert.Contains("Rendered from another session.", component.Markup, StringComparison.Ordinal));
    }

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
}

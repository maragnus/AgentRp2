using Microsoft.JSInterop;

namespace AgentRp.Services;

public interface ITtsAudioPlaybackService
{
    string ActiveKey { get; }
    event Func<Task>? Changed;
    event Func<string, string, Task>? Failed;
    bool IsPlaying(string key);
    bool TryGetCachedUrl(string key, out string url);
    Task CacheAudioAsync(string key, byte[] bytes, string contentType);
    Task PlayUrlAsync(string key, string url);
    Task StopAsync();
    Task ReplaceCachedAudioAsync(string key, byte[] bytes, string contentType);
}

public sealed class TtsAudioPlaybackService(IJSRuntime js) : ITtsAudioPlaybackService, IAsyncDisposable
{
    readonly Dictionary<string, string> cachedUrls = new(StringComparer.Ordinal);
    DotNetObjectReference<TtsAudioPlaybackService>? dotNetReference;

    public string ActiveKey { get; private set; } = "";
    public event Func<Task>? Changed;
    public event Func<string, string, Task>? Failed;

    public bool IsPlaying(string key) =>
        !string.IsNullOrWhiteSpace(key) && string.Equals(ActiveKey, key, StringComparison.Ordinal);

    public bool TryGetCachedUrl(string key, out string url) =>
        cachedUrls.TryGetValue(key, out url!);

    public async Task CacheAudioAsync(string key, byte[] bytes, string contentType)
    {
        if (cachedUrls.ContainsKey(key))
            return;

        cachedUrls[key] = await js.InvokeAsync<string>("agentRp.audio.createObjectUrl", bytes, contentType);
    }

    public async Task ReplaceCachedAudioAsync(string key, byte[] bytes, string contentType)
    {
        if (cachedUrls.Remove(key, out var previous))
            await js.InvokeVoidAsync("agentRp.audio.revokeObjectUrl", previous);

        cachedUrls[key] = await js.InvokeAsync<string>("agentRp.audio.createObjectUrl", bytes, contentType);
    }

    public async Task PlayUrlAsync(string key, string url)
    {
        dotNetReference ??= DotNetObjectReference.Create(this);
        ActiveKey = key;
        await NotifyChangedAsync();
        await js.InvokeVoidAsync("agentRp.audio.playUrl", key, url, dotNetReference);
    }

    public async Task StopAsync()
    {
        await js.InvokeVoidAsync("agentRp.audio.stop");
        ActiveKey = "";
        await NotifyChangedAsync();
    }

    [JSInvokable]
    public async Task NotifyAudioStopped(string key)
    {
        if (string.Equals(ActiveKey, key, StringComparison.Ordinal))
            ActiveKey = "";

        await NotifyChangedAsync();
    }

    [JSInvokable]
    public async Task NotifyAudioFailed(string key, string message)
    {
        if (string.Equals(ActiveKey, key, StringComparison.Ordinal))
            ActiveKey = "";

        var failed = Failed;
        if (failed is not null)
            await failed.Invoke(key, string.IsNullOrWhiteSpace(message) ? "Playing audio failed." : message);

        await NotifyChangedAsync();
    }

    async Task NotifyChangedAsync()
    {
        var changed = Changed;
        if (changed is not null)
            await changed.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var url in cachedUrls.Values)
            await js.InvokeVoidAsync("agentRp.audio.revokeObjectUrl", url);

        cachedUrls.Clear();
        dotNetReference?.Dispose();
    }
}

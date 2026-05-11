using AgentRp.Services;

namespace AgentRp.Session;

public interface IGlobalPromptLibraryStore
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    PromptLibraryState Snapshot();
    Task SaveAsync(PromptLibraryState state, CancellationToken cancellationToken = default);
}

public interface IGlobalModelTuningStore
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    ModelTuningState Snapshot();
    Task SaveAsync(ModelTuningState state, CancellationToken cancellationToken = default);
}

public sealed class GlobalPromptLibraryStore(IAppSettingsService appSettings) : IGlobalPromptLibraryStore
{
    public const string SettingsKey = "globalPromptLibrary";

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly object _stateLock = new();
    PromptLibraryState _state = PromptLibraryState.CreateDefault();
    bool _loaded;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
                return;

            var loaded = await appSettings.GetAsync(SettingsKey, PromptLibraryState.CreateDefault(), cancellationToken);
            lock (_stateLock)
                _state = PromptLibraryService.NormalizeState(SessionCloner.Clone(loaded));
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public PromptLibraryState Snapshot()
    {
        lock (_stateLock)
            return SessionCloner.Clone(PromptLibraryService.NormalizeState(_state));
    }

    public async Task SaveAsync(PromptLibraryState state, CancellationToken cancellationToken = default)
    {
        var normalized = PromptLibraryService.NormalizeState(SessionCloner.Clone(state));
        PromptLibraryService.ValidateState(normalized);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_stateLock)
                _state = normalized;
            await appSettings.SaveAsync(SettingsKey, Snapshot(), cancellationToken);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class GlobalModelTuningStore(IAppSettingsService appSettings) : IGlobalModelTuningStore
{
    public const string SettingsKey = "globalModelTuning";

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly object _stateLock = new();
    ModelTuningState _state = ModelTuningState.CreateDefault();
    bool _loaded;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
                return;

            var loaded = await appSettings.GetAsync(SettingsKey, ModelTuningState.CreateDefault(), cancellationToken);
            lock (_stateLock)
                _state = Normalize(SessionCloner.Clone(loaded));
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public ModelTuningState Snapshot()
    {
        lock (_stateLock)
            return SessionCloner.Clone(Normalize(_state));
    }

    public async Task SaveAsync(ModelTuningState state, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_stateLock)
                _state = Normalize(SessionCloner.Clone(state));
            await appSettings.SaveAsync(SettingsKey, Snapshot(), cancellationToken);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    static ModelTuningState Normalize(ModelTuningState state)
    {
        var defaults = ModelTuningState.CreateDefault();
        foreach (var pair in defaults.Values)
            state.Values.TryAdd(pair.Key, new()
            {
                Temperature = pair.Value.Temperature,
                TopP = pair.Value.TopP,
                MaxTokens = pair.Value.MaxTokens,
                Seed = pair.Value.Seed,
                FrequencyPenalty = pair.Value.FrequencyPenalty,
                PresencePenalty = pair.Value.PresencePenalty,
                StopSequences = pair.Value.StopSequences
            });
        return state;
    }
}

using System.Threading.Tasks;

namespace AgentRp.Session;

public sealed class GlobalModelTuningSessionStore(IGlobalModelTuningStore globalStore) : StoreBase
{
	public ModelTuningState State => globalStore.Snapshot();

	public async Task LoadAsync()
	{
		await globalStore.LoadAsync();
	}

	public async Task SaveAsync(ModelTuningState state)
	{
		await globalStore.SaveAsync(state);
		await NotifyChangedAsync();
	}
}

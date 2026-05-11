using System.Threading.Tasks;

namespace AgentRp.Session;

public sealed class GlobalPromptLibrarySessionStore(IGlobalPromptLibraryStore globalStore) : StoreBase
{
	public PromptLibraryState State => globalStore.Snapshot();

	public async Task LoadAsync()
	{
		await globalStore.LoadAsync();
	}

	public async Task SaveAsync(PromptLibraryState state)
	{
		await globalStore.SaveAsync(state);
		await NotifyChangedAsync();
	}

	public async Task ResetAllAsync()
	{
		await globalStore.SaveAsync(PromptLibraryState.CreateDefault());
		await NotifyChangedAsync();
	}
}

using System;
using System.Threading.Tasks;

namespace AgentRp.Session;

public sealed class ActiveChatContext
{
	public RpChatDocument? Current { get; private set; }

	public event Func<ActiveChatChange, Task>? Changed;

	public async Task ClearAsync()
	{
		Current = null;
		await NotifyAsync(null);
	}

	public async Task SetAsync(RpChatDocument document)
	{
		Current = document;
		await NotifyAsync(null);
	}

	public async Task UpdateAsync(RpChatDocument document, RoleplayStoreArea area)
	{
		Current = document;
		await NotifyAsync(area);
	}

	private async Task NotifyAsync(RoleplayStoreArea? area)
	{
		var changed = this.Changed;
		if (changed != null)
		{
			await changed(new ActiveChatChange(Current, area));
		}
	}
}

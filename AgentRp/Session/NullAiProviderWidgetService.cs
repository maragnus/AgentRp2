using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

internal sealed class NullAiProviderWidgetService : IAiProviderWidgetService
{
	public static NullAiProviderWidgetService Instance { get; } = new NullAiProviderWidgetService();

	public Task<IReadOnlyList<AiProviderMetric>> RefreshMetricsAsync(AiProvider provider, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.FromResult((IReadOnlyList<AiProviderMetric>)Array.Empty<AiProviderMetric>());
	}

	public Task<IReadOnlyList<ManagedEndpointStatusView>> GetHuggingFaceStatusesAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.FromResult((IReadOnlyList<ManagedEndpointStatusView>)Array.Empty<ManagedEndpointStatusView>());
	}

	public Task<ManagedEndpointStatusView> ExecuteHuggingFaceActionAsync(AiProvider provider, AiProviderModel model, ManagedEndpointAction action, CancellationToken cancellationToken = default(CancellationToken))
	{
		throw new NotSupportedException();
	}
}

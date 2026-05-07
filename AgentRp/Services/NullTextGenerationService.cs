using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed class NullTextGenerationService : ITextGenerationService
{
    public static NullTextGenerationService Instance { get; } = new();

    public Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Text generation is not available in this context.");

    public Task<GeneratedTurnResult> GenerateProseFromPlanAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateProseFromPlanRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Text generation is not available in this context.");

    public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateSnapshotRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Snapshot generation is not available in this context.");
}

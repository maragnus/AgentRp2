using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed class NullTextGenerationService : ITextGenerationService
{
    public static NullTextGenerationService Instance { get; } = new();

    public Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Text generation is not available in this context.");

    public Task<GeneratedTurnResult> GeneratePlanAndProseAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GeneratePlanAndProseRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Text generation is not available in this context.");

    public Task<GeneratedTurnResult> GenerateProseFromPlanAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateProseFromPlanRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Text generation is not available in this context.");

    public Task<CyoaActorSelection> SelectCyoaActorAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, SelectCyoaActorRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("CYOA actor selection is not available in this context.");

    public Task<GeneratedCyoaDecision> GenerateCyoaDecisionAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateCyoaDecisionRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("CYOA decision generation is not available in this context.");

    public Task<GeneratedTurnResult> GenerateSelectedCyoaTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSelectedCyoaTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("CYOA turn generation is not available in this context.");

    public Task<GeneratedTurnResult> GenerateAutonomousCyoaTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateAutonomousCyoaTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("CYOA turn generation is not available in this context.");

    public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSnapshotRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Snapshot generation is not available in this context.");
}

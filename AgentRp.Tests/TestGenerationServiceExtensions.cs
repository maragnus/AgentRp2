using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

internal static class TestGenerationServiceExtensions
{
    public static Task<GeneratedTurnResult> GenerateTurnAsync(
        this TextGenerationService service,
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateTurnRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default) =>
        service.GenerateTurnAsync(document, providers, ActiveModelSelectionsState.CreateDefault(), request, progress, cancellationToken);

    public static Task<GeneratedTurnResult> GenerateProseFromPlanAsync(
        this TextGenerationService service,
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateProseFromPlanRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default) =>
        service.GenerateProseFromPlanAsync(document, providers, ActiveModelSelectionsState.CreateDefault(), request, progress, cancellationToken);

    public static Task<GeneratedSnapshotResult> GenerateSnapshotAsync(
        this TextGenerationService service,
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateSnapshotRequest request,
        CancellationToken cancellationToken = default) =>
        service.GenerateSnapshotAsync(document, providers, ActiveModelSelectionsState.CreateDefault(), request, cancellationToken);
}

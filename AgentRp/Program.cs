using AgentRp.Components;
using AgentRp.Data;
using AgentRp.Services;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<RpDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("db")
        ?? throw new InvalidOperationException("Connection string 'db' was not found.");

    options.UseSqlServer(connectionString);
});

// Aspire's BlobContainerClient registration requires an explicit container name when using a storage-account
// connection string (ConnectionStrings:blobs). Production supplies an account connection string, not a
// container-scoped connection string, so set the container name here.
builder.AddAzureBlobContainerClient("blobs", settings => settings.BlobContainerName = "agentrp");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddTransient<ExternalApiLoggingHandler>();
builder.Services.AddHttpClient();
builder.Services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler<ExternalApiLoggingHandler>());
builder.Services.AddTinify();
builder.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
builder.Services.AddSingleton<IModelCapabilityCatalog, ModelCapabilityCatalog>();
builder.Services.AddSingleton<IAiProviderCapabilityPipeline, AiProviderCapabilityPipeline>();
builder.Services.AddSingleton<IModelClientFactory, ModelClientFactory>();
builder.Services.AddSingleton<IModelGenerationClient, OpenAiModelGenerationClient>();
builder.Services.AddScoped<IAiProviderConnectionService, AiProviderConnectionService>();
builder.Services.AddScoped<IAiProviderWidgetService, AiProviderWidgetService>();
builder.Services.AddScoped<IAiProviderVoiceDiscoveryService, AiProviderVoiceDiscoveryService>();
builder.Services.AddScoped<IAiProviderVoiceInventoryService, AiProviderVoiceInventoryService>();
builder.Services.AddScoped<IElevenLabsVoiceCatalogService, ElevenLabsVoiceCatalogService>();
builder.Services.AddSingleton<ISpeechGenerationService, SpeechGenerationService>();
builder.Services.AddScoped<ITtsPreviewService, TtsPreviewService>();
builder.Services.AddScoped<ITtsAudioPlaybackService, TtsAudioPlaybackService>();
builder.Services.AddScoped<IMessageSpeechService, MessageSpeechService>();
builder.Services.AddSingleton<IVoiceMessageStreamCoordinator, VoiceMessageStreamCoordinator>();
builder.Services.AddSingleton<IAudioTagGuideService, AudioTagGuideService>();
builder.Services.AddScoped<IEntityNotifier, EntityNotifier>();
builder.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
builder.Services.AddSingleton<IGlobalModelSelectionStore, GlobalModelSelectionStore>();
builder.Services.AddScoped<IImageGenerationService, ImageGenerationService>();
builder.Services.AddScoped<IImageBlobStorage, AzureImageBlobStorage>();
builder.Services.AddScoped<IStoredImageService, StoredImageService>();
builder.Services.AddScoped<IImageDetailsService, ImageDetailsService>();
builder.Services.AddScoped<IImageCropService, ImageCropService>();
builder.Services.AddScoped<DialogHelper>();
builder.Services.AddScoped<OverlayService>();
builder.Services.AddScoped<TranscriptPromptContextBuilder>();
builder.Services.AddSingleton<PromptLibraryService>();
builder.Services.AddSingleton<CharacterTraitLibraryService>();
builder.Services.AddScoped<SceneTransitionService>();
builder.Services.AddScoped<ITextGenerationService, TextGenerationService>();
builder.Services.AddScoped<StoryEntityPatchService>();
builder.Services.AddScoped<IStoryAssistantService, StoryAssistantService>();
builder.Services.AddSingleton<IRoleplayPersistence, SqlRoleplayPersistence>();
builder.Services.AddSingleton<ILiveRoleplayStore, LiveRoleplayStore>();
builder.Services.AddScoped<RoleplaySession>();
builder.Services.AddCascadingValue<RoleplaySession>(provider =>
    provider.GetRequiredService<RoleplaySession>());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RpDbContext>>();
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStoryImageEndpoints();

app.MapGet("/story-audio/{voiceMessageId}", async (
    string voiceMessageId,
    HttpContext context,
    IDbContextFactory<RpDbContext> dbContextFactory,
    IVoiceMessageStreamCoordinator streamCoordinator,
    CancellationToken cancellationToken) =>
{
    await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
    var audio = await dbContext.SpeechAssets
        .AsNoTracking()
        .Where(x => x.Id == voiceMessageId)
        .Select(x => new { x.Status, x.Bytes, x.ContentType, x.FileName, x.ErrorMessage, x.CreatedUtc, x.CompletedUtc })
        .FirstOrDefaultAsync(cancellationToken);

    if (audio is null)
        return Results.NotFound();

    if (audio.Status == SpeechAssetStatus.Ready && audio.Bytes.Length > 0)
    {
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Response.Headers.ETag = $"\"{voiceMessageId}-{audio.Bytes.Length}-{(audio.CompletedUtc ?? audio.CreatedUtc).Ticks}\"";
        return Results.File(audio.Bytes, audio.ContentType, audio.FileName, enableRangeProcessing: true);
    }

    if (audio.Status == SpeechAssetStatus.Failed)
        return Results.Problem(
            detail: string.IsNullOrWhiteSpace(audio.ErrorMessage) ? "Reading aloud failed while generating the audio." : audio.ErrorMessage,
            title: "Reading aloud failed",
            statusCode: StatusCodes.Status500InternalServerError);

    var start = await streamCoordinator.EnsureStartedAsync(voiceMessageId, cancellationToken);
    if (!start.Started)
        return Results.Problem(
            detail: start.ErrorMessage,
            title: "Reading aloud failed",
            statusCode: StatusCodes.Status500InternalServerError);

    context.Response.Headers.CacheControl = "no-store";
    return Results.Stream(
        stream => streamCoordinator.CopyLiveAsync(voiceMessageId, stream, context.RequestAborted),
        string.IsNullOrWhiteSpace(audio.ContentType) ? "audio/mpeg" : audio.ContentType,
        audio.FileName);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

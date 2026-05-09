using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentRp.Services;

public interface IElevenLabsVoiceCatalogService
{
    Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, CancellationToken cancellationToken = default);
    Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default);
    Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, CancellationToken cancellationToken = default);
    Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default);
    Task<ElevenLabsVoiceCatalogSnapshot> LoadSnapshotAsync(ElevenLabsVoiceCatalogFilter? filter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiProviderVoice>> LoadBookmarkedVoicesAsync(CancellationToken cancellationToken = default);
    Task SetBookmarkedAsync(string voiceId, bool bookmarked, CancellationToken cancellationToken = default);
}

public sealed class ElevenLabsVoiceCatalogService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<ElevenLabsVoiceCatalogService>? logger = null) : IElevenLabsVoiceCatalogService
{
    const string StateId = "global";
    readonly object gate = new();
    Task<ElevenLabsVoiceCatalogSnapshot>? refreshTask;

    public async Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.ElevenLabsVoiceCatalog.AnyAsync(cancellationToken))
            return await BuildSnapshotAsync(dbContext, ElevenLabsVoiceCatalogFilter.SearchAll, cancellationToken);

        return await RefreshAsync(provider, cancellationToken);
    }

    public async Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(
        AiProvider provider,
        IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.ElevenLabsVoiceCatalog.AnyAsync(cancellationToken))
            return await BuildSnapshotAsync(dbContext, ElevenLabsVoiceCatalogFilter.SearchAll, cancellationToken);

        return await RefreshAsync(provider, progress, cancellationToken);
    }

    public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            refreshTask ??= RefreshCoreAsync(provider, null, cancellationToken);
            return refreshTask;
        }
    }

    public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(
        AiProvider provider,
        IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            refreshTask ??= RefreshCoreAsync(provider, progress, cancellationToken);
            return refreshTask;
        }
    }

    public async Task<ElevenLabsVoiceCatalogSnapshot> LoadSnapshotAsync(
        ElevenLabsVoiceCatalogFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildSnapshotAsync(dbContext, filter ?? ElevenLabsVoiceCatalogFilter.SearchAll, cancellationToken);
    }

    public async Task<IReadOnlyList<AiProviderVoice>> LoadBookmarkedVoicesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.ElevenLabsVoiceCatalog
            .AsNoTracking()
            .Where(row => row.IsBookmarked)
            .OrderBy(row => row.Name)
            .ThenBy(row => row.VoiceId)
            .ToListAsync(cancellationToken);

        return rows.Select(ToProviderVoice).ToList();
    }

    public async Task SetBookmarkedAsync(string voiceId, bool bookmarked, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
            return;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ElevenLabsVoiceCatalog.FirstOrDefaultAsync(row => row.VoiceId == voiceId, cancellationToken);
        if (row is null)
            return;

        row.IsBookmarked = bookmarked;
        row.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    async Task<ElevenLabsVoiceCatalogSnapshot> RefreshCoreAsync(
        AiProvider provider,
        IProgress<ElevenLabsVoiceCatalogRefreshProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var state = await LoadStateAsync(dbContext, cancellationToken);
            try
            {
                var result = await DownloadCatalogAsync(provider, progress, cancellationToken);
                progress?.Report(new(result.PageCount, result.PageCount, result.Voices.Count, result.TotalCount, "Saving"));
                await ApplyRefreshAsync(dbContext, result, state, cancellationToken);
                progress?.Report(new(result.PageCount, result.PageCount, result.Voices.Count, result.TotalCount, "Complete"));
            }
            catch (Exception exception)
            {
                state.LastRefreshError = UserFacingErrorReporter.Capture(
                    logger,
                    exception,
                    "Refreshing the ElevenLabs voice catalog failed.",
                    "Refreshing ElevenLabs voice catalog failed for provider {ProviderId}.",
                    provider.Id);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return await BuildSnapshotAsync(dbContext, ElevenLabsVoiceCatalogFilter.SearchAll, cancellationToken);
        }
        finally
        {
            lock (gate)
                refreshTask = null;
        }
    }

    async Task<ElevenLabsCatalogDownload> DownloadCatalogAsync(
        AiProvider provider,
        IProgress<ElevenLabsVoiceCatalogRefreshProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var client = CreateElevenLabsClient(provider.ApiKey);
        var voices = new List<JsonNode>();
        var page = 0;
        var totalCount = 0;
        int? pageCount = null;
        bool hasMore;
        do
        {
            progress?.Report(new(page + 1, pageCount, voices.Count, totalCount == 0 ? null : totalCount, "Loading"));
            var path = $"shared-voices?page_size=100&page={page}";
            using var response = await client.GetAsync(new Uri(new Uri("https://api.elevenlabs.io/v1/"), path), cancellationToken);
            var json = await ReadJsonAsync(response, "Refreshing the ElevenLabs voice catalog", cancellationToken);
            totalCount = json["total_count"]?.GetValue<int>() ?? totalCount;
            voices.AddRange((json["voices"]?.AsArray() ?? []).Where(node => node is not null).Select(node => node!));
            pageCount = totalCount > 0 ? Math.Max(1, (int)Math.Ceiling(totalCount / 100.0)) : page + 1;
            hasMore = json["has_more"]?.GetValue<bool>() == true;
            progress?.Report(new(page + 1, pageCount, voices.Count, totalCount, "Loading"));
            page++;
        }
        while (hasMore);

        return new(voices, totalCount, pageCount ?? page);
    }

    async Task ApplyRefreshAsync(
        RpDbContext dbContext,
        ElevenLabsCatalogDownload result,
        ElevenLabsVoiceCatalogStateRow state,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rows = await dbContext.ElevenLabsVoiceCatalog.ToDictionaryAsync(row => row.VoiceId, StringComparer.Ordinal, cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in result.Voices)
        {
            var voiceId = ReadString(node, "voice_id");
            if (string.IsNullOrWhiteSpace(voiceId) || !seen.Add(voiceId))
                continue;

            if (!rows.TryGetValue(voiceId, out var row))
            {
                row = new() { VoiceId = voiceId, CreatedUtc = now };
                dbContext.ElevenLabsVoiceCatalog.Add(row);
                rows[voiceId] = row;
            }

            Apply(node, row, now);
        }

        foreach (var row in rows.Values.Where(row => !seen.Contains(row.VoiceId)))
        {
            row.IsAvailable = false;
            row.UpdatedUtc = now;
        }

        state.LastRefreshUtc = now;
        state.LastRefreshError = "";
        state.TotalCount = result.TotalCount;
        state.CachedCount = seen.Count;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    static void Apply(JsonNode node, ElevenLabsVoiceCatalogRow row, DateTime now)
    {
        row.PublicOwnerId = ReadString(node, "public_owner_id");
        row.DateUnix = ReadLong(node, "date_unix");
        row.Name = ReadString(node, "name");
        row.Accent = ReadString(node, "accent");
        row.Gender = ReadString(node, "gender");
        row.Age = ReadString(node, "age");
        row.Descriptive = ReadString(node, "descriptive");
        row.UseCase = ReadString(node, "use_case");
        row.Category = ReadString(node, "category");
        row.Language = ReadString(node, "language");
        row.Locale = ReadFirstVerifiedLanguageString(node, "locale");
        row.Description = ReadString(node, "description");
        row.PreviewUrl = ReadString(node, "preview_url");
        row.Featured = ReadBool(node, "featured");
        row.VerifiedLanguagesJson = node["verified_languages"]?.ToJsonString(AppJsonSerializerOptions.Web) ?? "[]";
        row.RawJson = node.ToJsonString(AppJsonSerializerOptions.Web);
        row.IsAvailable = true;
        row.LastSeenUtc = now;
        row.UpdatedUtc = now;
    }

    static async Task<ElevenLabsVoiceCatalogStateRow> LoadStateAsync(RpDbContext dbContext, CancellationToken cancellationToken)
    {
        var state = await dbContext.ElevenLabsVoiceCatalogStates.FirstOrDefaultAsync(row => row.Id == StateId, cancellationToken);
        if (state is not null)
            return state;

        state = new() { Id = StateId };
        dbContext.ElevenLabsVoiceCatalogStates.Add(state);
        return state;
    }

    static async Task<ElevenLabsVoiceCatalogSnapshot> BuildSnapshotAsync(
        RpDbContext dbContext,
        ElevenLabsVoiceCatalogFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ElevenLabsVoiceCatalog
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var state = await dbContext.ElevenLabsVoiceCatalogStates
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == StateId, cancellationToken);
        var availableRows = rows.Where(row => row.IsAvailable).ToList();
        var filtered = ApplyFilter(rows, filter)
            .OrderBy(row => row.Name)
            .ThenBy(row => row.VoiceId)
            .Select(ToEntry)
            .ToList();

        return new(
            filtered,
            Options(availableRows.Select(row => row.Accent)),
            Options(availableRows.Select(row => row.Gender)),
            Options(availableRows.Select(row => row.Age)),
            Options(availableRows.Select(row => row.UseCase)),
            Options(availableRows.Select(row => row.Category)),
            state?.LastRefreshUtc,
            state?.LastRefreshError ?? "",
            state?.TotalCount ?? 0,
            state?.CachedCount ?? availableRows.Count);
    }

    static IEnumerable<ElevenLabsVoiceCatalogRow> ApplyFilter(
        IReadOnlyList<ElevenLabsVoiceCatalogRow> rows,
        ElevenLabsVoiceCatalogFilter filter)
    {
        var query = string.Equals(filter.View, "bookmarked", StringComparison.OrdinalIgnoreCase)
            ? rows.Where(row => row.IsBookmarked)
            : rows.Where(row => row.IsAvailable);

        if (filter.FeaturedOnly)
            query = query.Where(row => row.Featured);
        if (!string.IsNullOrWhiteSpace(filter.Accent))
            query = query.Where(row => string.Equals(row.Accent, filter.Accent, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Gender))
            query = query.Where(row => string.Equals(row.Gender, filter.Gender, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Age))
            query = query.Where(row => string.Equals(row.Age, filter.Age, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.UseCase))
            query = query.Where(row => string.Equals(row.UseCase, filter.UseCase, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(row => string.Equals(row.Category, filter.Category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(row =>
                row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Descriptive.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    static IReadOnlyList<string> Options(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static AiProviderVoice ToProviderVoice(ElevenLabsVoiceCatalogEntry entry) => new()
    {
        Id = entry.VoiceId,
        DisplayName = entry.Name,
        Description = entry.Description,
        PreviewUrl = entry.PreviewUrl,
        Labels = Labels(entry),
        Source = "elevenlabs-catalog",
        IsCatalogVoice = true,
        IsBookmarked = entry.IsBookmarked,
        IsAvailable = entry.IsAvailable,
        UpdatedUtc = entry.UpdatedUtc
    };

    static AiProviderVoice ToProviderVoice(ElevenLabsVoiceCatalogRow row) => ToProviderVoice(ToEntry(row));

    static ElevenLabsVoiceCatalogEntry ToEntry(ElevenLabsVoiceCatalogRow row) => new()
    {
        VoiceId = row.VoiceId,
        PublicOwnerId = row.PublicOwnerId,
        Name = string.IsNullOrWhiteSpace(row.Name) ? row.VoiceId : row.Name,
        Description = row.Description,
        PreviewUrl = row.PreviewUrl,
        Featured = row.Featured,
        Accent = row.Accent,
        Gender = row.Gender,
        Age = row.Age,
        UseCase = row.UseCase,
        Category = row.Category,
        Language = row.Language,
        Locale = row.Locale,
        Descriptive = row.Descriptive,
        IsBookmarked = row.IsBookmarked,
        IsAvailable = row.IsAvailable,
        LastSeenUtc = row.LastSeenUtc,
        UpdatedUtc = row.UpdatedUtc
    };

    static Dictionary<string, string> Labels(ElevenLabsVoiceCatalogEntry entry)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(labels, "featured", entry.Featured ? "Featured" : "");
        Add(labels, "accent", entry.Accent);
        Add(labels, "gender", entry.Gender);
        Add(labels, "age", entry.Age);
        Add(labels, "use_case", entry.UseCase);
        Add(labels, "category", entry.Category);
        return labels;
    }

    static void Add(Dictionary<string, string> labels, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            labels[key] = DisplayLabel(value);
    }

    static string DisplayLabel(string value) =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').Replace('-', ' '));

    HttpClient CreateElevenLabsClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        return client;
    }

    static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonNode>(AppJsonSerializerOptions.Web, cancellationToken) ?? new JsonObject();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ExternalServiceFailureException(
            UserFacingErrorMessageBuilder.BuildExternalHttpFailure(operation, response.StatusCode, responseBody, "ElevenLabs"),
            response.StatusCode,
            responseBody);
    }

    static string ReadString(JsonNode? node, string property)
    {
        var value = node?[property];
        if (value is null)
            return "";

        try
        {
            return value.GetValue<string>() ?? "";
        }
        catch (InvalidOperationException)
        {
            return value.ToString();
        }
    }

    static long? ReadLong(JsonNode? node, string property)
    {
        var value = node?[property];
        if (value is null)
            return null;

        try
        {
            return value.GetValue<long>();
        }
        catch (InvalidOperationException)
        {
            return long.TryParse(value.GetValue<string>(), out var parsed) ? parsed : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    static bool ReadBool(JsonNode? node, string property)
    {
        var value = node?[property];
        if (value is null)
            return false;

        try
        {
            return value.GetValue<bool>();
        }
        catch (InvalidOperationException)
        {
            return bool.TryParse(value.GetValue<string>(), out var parsed) && parsed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    static string ReadFirstVerifiedLanguageString(JsonNode? node, string property) =>
        node?["verified_languages"]?.AsArray()
            .Select(item => ReadString(item, property))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?? "";

    sealed record ElevenLabsCatalogDownload(IReadOnlyList<JsonNode> Voices, int TotalCount, int PageCount);
}

public static class AiProviderVoiceMergeRules
{
    public static IReadOnlyList<AiProviderVoice> MergeProviderAndCatalogVoices(
        IReadOnlyList<AiProviderVoice> providerVoices,
        IReadOnlyList<AiProviderVoice> catalogVoices)
    {
        var catalogById = catalogVoices.ToDictionary(voice => voice.Id, StringComparer.Ordinal);
        var merged = providerVoices.Select(voice => Merge(voice, catalogById.GetValueOrDefault(voice.Id))).ToList();
        var providerIds = providerVoices.Select(voice => voice.Id).ToHashSet(StringComparer.Ordinal);
        merged.AddRange(catalogVoices.Where(voice => !providerIds.Contains(voice.Id)).Select(Clone));

        return merged
            .DistinctBy(voice => voice.Id, StringComparer.Ordinal)
            .OrderBy(voice => AiProviderVoiceDisplayName(voice), StringComparer.OrdinalIgnoreCase)
            .ThenBy(voice => voice.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static AiProviderVoice Merge(AiProviderVoice providerVoice, AiProviderVoice? catalogVoice)
    {
        var merged = Clone(providerVoice);
        if (catalogVoice is null)
            return merged;

        merged.IsCatalogVoice = true;
        merged.IsBookmarked = catalogVoice.IsBookmarked;
        merged.IsAvailable = catalogVoice.IsAvailable;
        if (string.IsNullOrWhiteSpace(merged.Description))
            merged.Description = catalogVoice.Description;
        if (string.IsNullOrWhiteSpace(merged.PreviewUrl))
            merged.PreviewUrl = catalogVoice.PreviewUrl;
        foreach (var pair in catalogVoice.Labels)
        {
            if (!merged.Labels.ContainsKey(pair.Key))
                merged.Labels[pair.Key] = pair.Value;
        }

        return merged;
    }

    static AiProviderVoice Clone(AiProviderVoice voice) => new()
    {
        Id = voice.Id,
        DisplayName = voice.DisplayName,
        Description = voice.Description,
        PreviewUrl = voice.PreviewUrl,
        Labels = voice.Labels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        Source = voice.Source,
        IsCatalogVoice = voice.IsCatalogVoice,
        IsBookmarked = voice.IsBookmarked,
        IsAvailable = voice.IsAvailable,
        UpdatedUtc = voice.UpdatedUtc
    };

    static string AiProviderVoiceDisplayName(AiProviderVoice voice) =>
        string.IsNullOrWhiteSpace(voice.DisplayName) ? voice.Id : voice.DisplayName;
}

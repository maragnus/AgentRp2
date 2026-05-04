using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public sealed class CharacterStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Characters;
    public List<RpCharacter> Items => Document?.Characters ?? [];

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) =>
        changedArea is null || changedArea == Area || changedArea == RoleplayStoreArea.Transcript;

    public async Task<RpCharacter> AddAsync()
    {
        var character = new RpCharacter { Id = NextId(), Name = "New Character" };
        Items.Insert(0, character);
        await SaveActiveDocumentAsync();
        return character;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(character => character.Id == id);
        if (Document is not null)
        {
            RemoveCharacterReferences(Document.Transcript.RootScene, id);
            foreach (var turn in Document.Transcript.Turns)
            {
                RemoveCharacterReferences(turn.Scene, id);
                turn.AppearanceByCharacterId.Remove(id);
                turn.PrivateIntentByCharacterId.Remove(id);
            }

            foreach (var snapshot in Document.Transcript.Snapshots)
                snapshot.CharacterAppearances.Remove(id);

            TranscriptProjector.Apply(Document);
            await SaveCatalogAndTranscriptAsync();
            return;
        }

        await SaveActiveDocumentAsync();
    }

    public async Task ToggleInSceneAsync(string id)
    {
        if (Document is null)
            return;

        var scene = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        if (!scene.InSceneCharacterIds.Remove(id))
            scene.InSceneCharacterIds.Add(id);

        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task SetImageAsync(string id, string imageId)
    {
        Items.First(character => character.Id == id).ImageId = imageId;
        await SaveActiveDocumentAsync();
    }

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    async Task SaveCatalogAndTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    static void RemoveCharacterReferences(RpSceneFrame scene, string id) =>
        scene.InSceneCharacterIds.RemoveAll(characterId => characterId == id);

    string NextId() => NextIdFor(Items.Select(character => character.Id), "c");

    static string NextIdFor(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class LocationStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Locations;
    public List<RpLocation> Items => Document?.Locations ?? [];
    public RpLocation? Active => Items.FirstOrDefault(location => location.IsActive);

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) =>
        changedArea is null || changedArea == Area || changedArea == RoleplayStoreArea.Transcript;

    public async Task<RpLocation> AddAsync()
    {
        var location = new RpLocation { Id = NextId(), Name = "New Location", Summary = "New location summary." };
        Items.Add(location);
        await SaveActiveDocumentAsync();
        return location;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(location => location.Id == id);
        var replacement = Items.FirstOrDefault();
        if (Document is not null)
        {
            UpdateScene(Document.Transcript.RootScene, id, replacement);
            foreach (var turn in Document.Transcript.Turns)
                UpdateScene(turn.Scene, id, replacement);

            foreach (var snapshot in Document.Transcript.Snapshots)
                UpdateScene(snapshot.Scene, id, replacement);

            TranscriptProjector.Apply(Document);
            await SaveCatalogAndTranscriptAsync();
            return;
        }

        await SaveActiveDocumentAsync();
    }

    public async Task SetActiveAsync(string id)
    {
        if (Document is null)
            return;

        var scene = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        var location = Items.FirstOrDefault(item => item.Id == id);
        scene.LocationId = location?.Id ?? "";
        scene.LocationName = location?.Name ?? "";
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task SetImageAsync(string id, string imageId)
    {
        Items.First(location => location.Id == id).ImageId = imageId;
        await SaveActiveDocumentAsync();
    }

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    async Task SaveCatalogAndTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    static void UpdateScene(RpSceneFrame scene, string deletedId, RpLocation? replacement)
    {
        if (scene.LocationId != deletedId)
            return;

        scene.LocationId = replacement?.Id ?? "";
        scene.LocationName = replacement?.Name ?? "";
    }

    string NextId() => NextIdFor(Items.Select(location => location.Id), "l");

    static string NextIdFor(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class ItemStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Items;
    public List<RpItem> Items => Document?.Items ?? [];

    protected override bool ShouldHandleArea(RoleplayStoreArea? changedArea) =>
        changedArea is null || changedArea == Area || changedArea == RoleplayStoreArea.Transcript;

    public async Task<RpItem> AddAsync()
    {
        var item = new RpItem { Id = NextId(), Name = "New Item", Summary = "New item summary." };
        Items.Add(item);
        await SaveActiveDocumentAsync();
        return item;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(item => item.Id == id);
        if (Document is not null)
        {
            Document.Transcript.RootScene.InSceneItemIds.RemoveAll(itemId => itemId == id);
            foreach (var turn in Document.Transcript.Turns)
                turn.Scene.InSceneItemIds.RemoveAll(itemId => itemId == id);

            foreach (var snapshot in Document.Transcript.Snapshots)
                snapshot.Scene.InSceneItemIds.RemoveAll(itemId => itemId == id);

            TranscriptProjector.Apply(Document);
            await SaveCatalogAndTranscriptAsync();
            return;
        }

        await SaveActiveDocumentAsync();
    }

    public async Task ToggleInSceneAsync(string id)
    {
        if (Document is null)
            return;

        var scene = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        if (!scene.InSceneItemIds.Remove(id))
            scene.InSceneItemIds.Add(id);

        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task SetImageAsync(string id, string imageId)
    {
        Items.First(item => item.Id == id).ImageId = imageId;
        await SaveActiveDocumentAsync();
    }

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    async Task SaveCatalogAndTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    string NextId() => NextIdFor(Items.Select(item => item.Id), "i");

    static string NextIdFor(IEnumerable<string> ids, string prefix)
    {
        var next = ids
            .Where(id => id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(id[prefix.Length..], out _))
            .Select(id => int.Parse(id[prefix.Length..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}{next}";
    }
}

public sealed class TimelineStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Timeline;
    public List<RpTimelineEntry> Items => Document?.Timeline ?? [];

    public async Task<RpTimelineEntry> AddAsync()
    {
        var entry = new RpTimelineEntry { Id = NextId(), Title = "New Event", Date = "today" };
        Items.Add(entry);
        await SaveActiveDocumentAsync();
        return entry;
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(entry => entry.Id == id);
        await SaveActiveDocumentAsync();
    }

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    string NextId()
    {
        var next = Items
            .Select(entry => entry.Id)
            .Where(id => id.Length > 1 && id.StartsWith("t", StringComparison.OrdinalIgnoreCase) && int.TryParse(id[1..], out _))
            .Select(id => int.Parse(id[1..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"t{next}";
    }
}

public sealed class ImageStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Images;
    public List<GalleryImage> Items => Document?.Images ?? [];

    public async Task AddAsync(GalleryImage image)
    {
        Items.Insert(0, image);
        await SaveActiveDocumentAsync();
    }

    public async Task DeleteAsync(string id)
    {
        Items.RemoveAll(image => image.Id == id);
        await SaveActiveDocumentAsync();
    }

    public string NextGalleryImageId()
    {
        var index = Items
            .Select(image => image.Id)
            .Where(id => id.Length > 1 && id[0] == 'g' && int.TryParse(id[1..], out _))
            .Select(id => int.Parse(id[1..]))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"g{index}";
    }
}

public sealed class TranscriptStore(
    ActiveChatContext activeChat,
    ChatRegistry registry,
    ProviderStore providers,
    ITextGenerationService textGenerationService) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Transcript;

    public RpTranscriptState State => Document?.Transcript ?? new();
    public List<RpTranscriptTurn> Items => Document is null ? [] : TranscriptGraph.GetActivePath(Document.Transcript);
    public RpTranscriptTurn? ActiveLeaf => Document is null ? null : TranscriptGraph.FindTurn(Document.Transcript, Document.Transcript.ActiveLeafTurnId);

    public RpTranscriptSnapshot? SnapshotFor(string turnId) =>
        Document is null ? null : TranscriptGraph.FindSnapshotByTurn(Document.Transcript, turnId);

    public IReadOnlyList<RpTranscriptTurn> SiblingsFor(string turnId) =>
        Document is null ? [] : TranscriptGraph.GetSiblings(Document.Transcript, turnId);

    public async Task PostManualAsync(string text, RpCharacter? speaker)
    {
        if (Document is null || string.IsNullOrWhiteSpace(text))
            return;

        ClearBackgroundError();
        var now = DateTime.UtcNow;
        var authorName = speaker?.Name ?? "Narrator";
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = Document.Transcript.ActiveLeafTurnId,
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "manual",
            AuthorCharacterId = speaker?.Id ?? "",
            AuthorName = authorName,
            ActorCharacterId = speaker?.Id ?? "",
            ActorName = authorName,
            Body = text.Trim(),
            Scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(Document.Transcript))
        };
        CommitTurn(turn, now);
        await SaveTranscriptAsync();
    }

    public async Task GenerateAsync(string guidance, RpCharacter? requestedActor, string mode, string turnShape)
    {
        if (Document is null)
            return;

        await GenerateTurnCoreAsync(
            parentTurnId: Document.Transcript.ActiveLeafTurnId,
            guidance,
            requestedActor,
            turnShape,
            mode);
    }

    public async Task RegenerateAsync(string turnId, string guidance, RpCharacter? requestedActor, string turnShape)
    {
        if (Document is null)
            return;

        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var actor = requestedActor ?? Document.Characters.FirstOrDefault(character => character.Id == original.ActorCharacterId);
        await GenerateTurnCoreAsync(
            parentTurnId: original.ParentTurnId,
            guidance: string.IsNullOrWhiteSpace(guidance) ? original.Guidance : guidance,
            requestedActor: actor,
            turnShape: string.IsNullOrWhiteSpace(turnShape) ? original.Plan.TurnShape : turnShape,
            mode: "regenerated");
    }

    public async Task EditTurnAsync(
        string turnId,
        string body,
        RpTurnPlan? plan = null,
        IReadOnlyDictionary<string, string>? appearances = null,
        IReadOnlyDictionary<string, string>? privateIntents = null)
    {
        if (Document is null || string.IsNullOrWhiteSpace(body))
            return;

        ClearBackgroundError();
        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var now = DateTime.UtcNow;
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = original.ParentTurnId,
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "edited",
            AuthorCharacterId = original.AuthorCharacterId,
            AuthorName = original.AuthorName,
            ActorCharacterId = original.ActorCharacterId,
            ActorName = original.ActorName,
            Guidance = original.Guidance,
            Body = body.Trim(),
            Plan = plan is null ? SessionCloner.Clone(original.Plan) : SessionCloner.Clone(plan),
            AppearanceByCharacterId = CloneMap(appearances ?? original.AppearanceByCharacterId),
            PrivateIntentByCharacterId = CloneMap(privateIntents ?? original.PrivateIntentByCharacterId),
            Scene = SessionCloner.Clone(original.Scene)
        };
        CommitTurn(turn, now);
        await SaveTranscriptAsync();
    }

    public async Task RecastTurnAsync(string turnId, RpCharacter? author)
    {
        if (Document is null)
            return;

        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var now = DateTime.UtcNow;
        var authorName = author?.Name ?? "Narrator";
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = original.ParentTurnId,
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "edited",
            AuthorCharacterId = author?.Id ?? "",
            AuthorName = authorName,
            ActorCharacterId = author?.Id ?? "",
            ActorName = authorName,
            Guidance = original.Guidance,
            Body = original.Body,
            Plan = SessionCloner.Clone(original.Plan),
            AppearanceByCharacterId = CloneMap(original.AppearanceByCharacterId),
            PrivateIntentByCharacterId = CloneMap(original.PrivateIntentByCharacterId),
            Scene = SessionCloner.Clone(original.Scene)
        };
        CommitTurn(turn, now);
        await SaveTranscriptAsync();
    }

    public async Task SavePlanAsync(
        string turnId,
        RpTurnPlan plan,
        IReadOnlyDictionary<string, string> appearances,
        IReadOnlyDictionary<string, string> privateIntents)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var turn = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (turn is null)
            return;

        turn.Plan = SessionCloner.Clone(plan);
        turn.AppearanceByCharacterId = CloneMap(appearances);
        turn.PrivateIntentByCharacterId = CloneMap(privateIntents);
        turn.UpdatedUtc = DateTime.UtcNow;
        await SaveTranscriptAsync();
    }

    public async Task CreateSnapshotAsync(string turnId)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateSnapshotAsync(
                Document,
                providers.Items.ToList(),
                new(turnId));
            var snapshot = TranscriptGraph.FindSnapshotByTurn(Document.Transcript, turnId) ?? new RpTranscriptSnapshot { Id = NextSnapshotId(), TurnId = turnId };
            snapshot.CreatedUtc = DateTime.UtcNow;
            snapshot.Summary = result.Summary;
            snapshot.EarlierPrivateIntentContinuity = result.EarlierPrivateIntentContinuity;
            snapshot.CharacterAppearances = CloneMap(result.CharacterAppearances);
            snapshot.Scene = SessionCloner.Clone(result.Scene);
            snapshot.Trace = SessionCloner.Clone(result.Trace);
            if (Document.Transcript.Snapshots.All(existing => existing.Id != snapshot.Id))
                Document.Transcript.Snapshots.Add(snapshot);

            var turn = TranscriptGraph.FindTurn(Document.Transcript, turnId);
            if (turn is not null)
            {
                turn.SnapshotId = snapshot.Id;
                turn.UpdatedUtc = DateTime.UtcNow;
            }

            TranscriptProjector.Apply(Document);
            await SaveTranscriptAsync();
        }
        catch (TranscriptGenerationException exception)
        {
            CaptureBackgroundError(exception);
            await NotifyChangedAsync();
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            await NotifyChangedAsync();
        }
    }

    public async Task SelectSiblingAsync(string turnId)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        if (TranscriptGraph.FindTurn(Document.Transcript, turnId) is null)
            return;

        TranscriptGraph.SelectLeaf(Document.Transcript, ResolveLeafFrom(turnId));
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task DeleteTurnAsync(string id)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var turn = TranscriptGraph.FindTurn(Document.Transcript, id);
        if (turn is null)
            return;

        var children = TranscriptGraph.GetChildren(Document.Transcript, turn.Id);
        foreach (var child in children)
            child.ParentTurnId = turn.ParentTurnId;

        Document.Transcript.Snapshots.RemoveAll(snapshot => snapshot.TurnId == id);
        Document.Transcript.Turns.RemoveAll(existing => existing.Id == id);
        if (Document.Transcript.ActiveLeafTurnId == id)
            Document.Transcript.ActiveLeafTurnId = children.LastOrDefault()?.Id ?? turn.ParentTurnId;

        TranscriptGraph.RepairSelections(Document.Transcript);
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task DeleteBranchAsync(string id)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var toDelete = CollectSubtreeIds(id);
        if (toDelete.Count == 0)
            return;

        var parentId = TranscriptGraph.FindTurn(Document.Transcript, id)?.ParentTurnId ?? "";
        Document.Transcript.Snapshots.RemoveAll(snapshot => toDelete.Contains(snapshot.TurnId));
        Document.Transcript.Turns.RemoveAll(turn => toDelete.Contains(turn.Id));
        if (toDelete.Contains(Document.Transcript.ActiveLeafTurnId))
            Document.Transcript.ActiveLeafTurnId = TranscriptGraph.GetChildren(Document.Transcript, parentId).LastOrDefault()?.Id ?? parentId;

        TranscriptGraph.RepairSelections(Document.Transcript);
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    public async Task ApplySceneStateAsync(RpSceneFrame scene)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var target = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
        target.LocationId = scene.LocationId;
        target.LocationName = scene.LocationName;
        target.InSceneCharacterIds = [.. scene.InSceneCharacterIds];
        target.InSceneItemIds = [.. scene.InSceneItemIds];
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    }

    async Task GenerateTurnCoreAsync(string parentTurnId, string guidance, RpCharacter? requestedActor, string turnShape, string mode)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateTurnAsync(
                Document,
                providers.Items.ToList(),
                new(
                    parentTurnId,
                    mode,
                    guidance,
                    turnShape,
                    requestedActor?.Id ?? "",
                    requestedActor?.Name ?? ""));
            result.Trace.Data["actorName"] = result.ActorName;
            var now = DateTime.UtcNow;
            var turn = new RpTranscriptTurn
            {
                Id = NextTurnId(),
                ParentTurnId = parentTurnId,
                CreatedUtc = now,
                UpdatedUtc = now,
                Mode = NormalizeMode(mode),
                AuthorCharacterId = result.ActorCharacterId,
                AuthorName = string.IsNullOrWhiteSpace(result.ActorName) ? "Narrator" : result.ActorName,
                ActorCharacterId = result.ActorCharacterId,
                ActorName = result.ActorName,
                Guidance = guidance.Trim(),
                Body = result.Body,
                Plan = SessionCloner.Clone(result.Plan),
                AppearanceByCharacterId = CloneMap(result.AppearanceByCharacterId),
                PrivateIntentByCharacterId = CloneMap(result.PrivateIntentByCharacterId),
                Scene = SessionCloner.Clone(result.Scene),
                Trace = SessionCloner.Clone(result.Trace)
            };
            CommitTurn(turn, now);
            await SaveTranscriptAsync();
        }
        catch (TranscriptGenerationException exception)
        {
            PersistFailedTurn(parentTurnId, guidance, requestedActor, mode, exception.Trace);
            CaptureBackgroundError(exception);
            await SaveTranscriptAsync();
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            await NotifyChangedAsync();
        }
    }

    void PersistFailedTurn(string parentTurnId, string guidance, RpCharacter? requestedActor, string mode, RpTurnTrace trace)
    {
        if (Document is null)
            return;

        trace.Data["actorName"] = requestedActor?.Name ?? "Narrator";
        var now = DateTime.UtcNow;
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = parentTurnId,
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = NormalizeMode(mode),
            AuthorCharacterId = requestedActor?.Id ?? "",
            AuthorName = requestedActor?.Name ?? "Narrator",
            ActorCharacterId = requestedActor?.Id ?? "",
            ActorName = requestedActor?.Name ?? "Narrator",
            Guidance = guidance.Trim(),
            Scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(Document.Transcript)),
            Trace = SessionCloner.Clone(trace)
        };
        CommitTurn(turn, now);
    }

    void CommitTurn(RpTranscriptTurn turn, DateTime now)
    {
        if (Document is null)
            return;

        Document.Transcript.Turns.Add(turn);
        TranscriptGraph.SelectLeaf(Document.Transcript, turn.Id);
        TranscriptProjector.Apply(Document, now);
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        TranscriptProjector.Apply(Document);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    HashSet<string> CollectSubtreeIds(string rootId)
    {
        if (Document is null)
            return [];

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!ids.Add(current))
                continue;

            foreach (var child in TranscriptGraph.GetChildren(Document.Transcript, current))
                stack.Push(child.Id);
        }

        return ids;
    }

    string ResolveLeafFrom(string turnId)
    {
        if (Document is null)
            return turnId;

        var currentId = turnId;
        while (true)
        {
            var children = TranscriptGraph.GetChildren(Document.Transcript, currentId);
            if (children.Count == 0)
                return currentId;

            var selectionKey = TranscriptGraph.BranchKey(currentId);
            var selectedChild = Document.Transcript.BranchSelections.TryGetValue(selectionKey, out var selectedId)
                ? children.FirstOrDefault(child => child.Id == selectedId)
                : null;
            currentId = (selectedChild ?? children.Last()).Id;
        }
    }

    static Dictionary<string, string> CloneMap(IReadOnlyDictionary<string, string> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    static string NormalizeMode(string mode) => mode switch
    {
        "guided" => "guided",
        "automatic" => "automatic",
        "regenerated" => "regenerated",
        "edited" => "edited",
        _ => "manual"
    };

    static string NextTurnId() => $"turn-{Guid.NewGuid():N}";
    static string NextSnapshotId() => $"snap-{Guid.NewGuid():N}";
}

public sealed class PromptLibraryStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.PromptLibrary;
    public PromptLibraryState State => EnsureDefaults(Document?.PromptLibrary ?? PromptLibraryState.CreateDefault());

    public IReadOnlyDictionary<string, PromptPairState> Prompts => State.Prompts;
    public IReadOnlyDictionary<string, List<ShapePromptState>> TurnShapes => State.TurnShapes;

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    public void ResetPrompt(string stepId, string field)
    {
        var defaults = PromptLibraryState.CreateDefault();
        if (field == "system")
            State.Prompts[stepId].System = defaults.Prompts[stepId].System;
        else
            State.Prompts[stepId].User = defaults.Prompts[stepId].User;
    }

    public void ResetTurnShape(string stepId, string shapeId)
    {
        var defaults = PromptLibraryState.CreateDefault();
        State.TurnShapes[stepId].First(shape => shape.Id == shapeId).Value = defaults.TurnShapes[stepId].First(shape => shape.Id == shapeId).Value;
    }

    static PromptLibraryState EnsureDefaults(PromptLibraryState state)
    {
        var defaults = PromptLibraryState.CreateDefault();
        foreach (var pair in defaults.Prompts)
            state.Prompts.TryAdd(pair.Key, new PromptPairState { System = pair.Value.System, User = pair.Value.User });

        foreach (var pair in defaults.TurnShapes)
        {
            if (!state.TurnShapes.ContainsKey(pair.Key))
                state.TurnShapes[pair.Key] = pair.Value.Select(shape => new ShapePromptState { Id = shape.Id, Label = shape.Label, Value = shape.Value }).ToList();
        }

        return state;
    }
}

public sealed class ModelTuningStore(ActiveChatContext activeChat, ChatRegistry registry) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.ModelTuning;
    public ModelTuningState State => EnsureDefaults(Document?.ModelTuning ?? ModelTuningState.CreateDefault());
    public IReadOnlyDictionary<string, ModelTuningStepState> Values => State.Values;

    public Task MarkChangedAsync() => SaveActiveDocumentAsync();

    public void Reset(string stepId)
    {
        State.Values[stepId] = SessionCloner.Clone(ModelTuningState.CreateDefault()).Values[stepId];
    }

    static ModelTuningState EnsureDefaults(ModelTuningState state)
    {
        var defaults = ModelTuningState.CreateDefault();
        foreach (var pair in defaults.Values)
            state.Values.TryAdd(pair.Key, new ModelTuningStepState
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

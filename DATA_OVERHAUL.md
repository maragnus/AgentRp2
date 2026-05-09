# Data Overhaul

This document captures the current direction for moving AgentRp2 away from large
chat-level `*Json` blobs and toward a queryable, efficient, DRY persistence
model. The transcript overhaul has its own complexity and should be expanded in
a follow-up pass; this document focuses on the rest of the chat/story data.

## Why This Exists

The current data model was useful while the app was moving quickly, but it now
creates real product and engineering costs:

- Chat lists are hot UI paths, but preview data is partly denormalized and
  partly reconstructed from hydrated chat documents.
- The sidebar story list and startup story resume cards drifted because they use
  different rendering paths and different assumptions about preview data.
- Loading an active chat can require deserializing large JSON blocks that are not
  needed for first paint.
- Saving small changes can rewrite large unrelated JSON blocks.
- Queryable data such as active location, scene characters, avatar image ids,
  crop data, starred state, dates, and counts is trapped in blobs or duplicated
  into loosely owned preview fields.

The fix is not just "make the card use the same avatar component." That is the
symptom. The root problem is that the app does not have a first-class,
renderable, queryable story preview model.

## Current Shape

Current storage is roughly:

- `Chats`
  - scalar list fields such as title, location, updated text, message count,
    starred state, sort order, and last message date.
  - preview blobs: `ActiveLocationJson`, `SceneCharactersJson`.
- `ChatDocuments`
  - aggregate-level blobs:
    - `CharactersJson`
    - `CharacterRelationshipsJson`
    - `LocationsJson`
    - `ItemsJson`
    - `TimelineJson`
    - `ImagesJson`
    - `MessagesJson`
    - `StoryAssistantJson`
    - `ChatDirectionJson`
    - `NarratorProfileJson`
    - `PromptLibraryJson`
    - `CharacterTraitLibraryJson`
    - `ModelTuningJson`
- `ImageAssets`
  - actual stored image metadata/blob references, including crop fields.
- `SpeechAssets`
  - generated audio state and bytes.

The in-memory `RpChatDocument` is a full aggregate containing chat metadata,
characters, locations, items, images, transcript, assistant state, direction,
narrator profile, prompt library, trait library, and model tuning.

`RpChat` is currently doing double duty:

- It is the chat list item.
- It is also the story preview carrier.
- It contains denormalized `ActiveLocation` and `SceneCharacters` data for
  unhydrated rendering.

That is too vague. A preview should be a first-class read model, not a side
effect on the chat entity.

## Design Goals

- Listing all chats should be cheap and not require loading chat documents.
- Chat list rows and startup resume cards must render from the same read model.
- Preview rows must include active/last scene state:
  - active location name
  - active location avatar image id/url/crop details
  - in-scene character names
  - in-scene character avatar image id/url/crop details
- Maintaining preview state should be low overhead.
- Saving a small field should not rewrite unrelated large JSON blocks.
- Loading an active chat should be staged and fast:
  - first load: shell, metadata, active scene preview, visible transcript path
  - later/lazy load: detailed profiles, traces, assistant history, old branches,
    large prompt/debug payloads
- Use relational fields/tables for frequently queried or frequently changed
  data.
- Use JSON blobs for rarely queried, bulky, mostly static detail payloads.
- Keep UI DRY by giving all story preview surfaces the same model and component
  contract.

## Proposed Principle

Use a hybrid relational/document model:

- Hot/queryable/changeable fields become columns and rows.
- Cold/rich/detail fields stay as JSON payloads owned by the relevant row.
- Derived display state gets explicit read models/tables.
- The app stops treating one giant chat document as the only persistence unit.

This is not a compatibility migration project. AgentRp2 is still in design, so
breaking structural changes are acceptable when they produce the correct model.

## First-Class Story Preview

Introduce a read model shaped for rendering, for example:

```csharp
public sealed class StoryPreview
{
    public string ChatId { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Starred { get; set; }
    public int VisibleTurnCount { get; set; }
    public int LastGeneratedTurnNumber { get; set; }
    public DateTime? LastMessageUtc { get; set; }
    public StoryPreviewLocation? ActiveLocation { get; set; }
    public List<StoryPreviewCharacter> SceneCharacters { get; set; } = [];
}

public sealed class StoryPreviewLocation
{
    public string LocationId { get; set; } = "";
    public string Name { get; set; } = "";
    public StoryPreviewAvatar? Avatar { get; set; }
}

public sealed class StoryPreviewCharacter
{
    public string CharacterId { get; set; } = "";
    public string Name { get; set; } = "";
    public StoryPreviewAvatar? Avatar { get; set; }
}

public sealed class StoryPreviewAvatar
{
    public string ImageId { get; set; } = "";
    public string Url { get; set; } = "";
    public int FocusXPercent { get; set; }
    public int FocusYPercent { get; set; }
    public int ZoomPercent { get; set; }
}
```

The exact class names can change, but the contract matters:

- Sidebar story list rows consume this model.
- Startup story resume cards consume this model.
- Active story picker consumes this model.
- Shared avatar stack/rendering is owned by a common component.
- Feature components do not independently rebuild avatar logic.

This is the DRY fix. The UI should not know whether preview data came from a
hydrated active chat, a query, or a cached projection.

## Target Tables Outside Transcript

### Chats

Keep `Chats` as the root story row and source for cheap top-level listing.

Likely columns:

- `Id`
- `Title`
- `Starred`
- `SortOrder`
- `CreatedUtc`
- `UpdatedUtc`
- `LastMessageUtc`
- `LastGeneratedTurnNumber`
- `VisibleTurnCount`
- `ActiveLeafTurnId` later, as part of transcript work
- `ActiveLocationId`
- `ActiveLocationName`
- `PreviewVersion` or `ProjectionVersion`

Do not keep `ActiveLocationJson` or `SceneCharactersJson` long term. They are
useful proof that a preview read model is needed, but not the destination.

### ChatCharacters

Break `CharactersJson` into rows.

Hot/queryable columns:

- `ChatId`
- `Id`
- `Name`
- `ImageId`
- `SortOrder`
- `InScene` only if it remains useful as cached preview state; otherwise scene
  membership should come from current scene tables.
- `UpdatedUtc`

Cold/detail JSON:

- `ProfileJson`
  - summary
  - personality
  - appearance details
  - backstory
  - notes
  - pronouns
  - scene roles
  - traits/drives/limits
  - deep character profile fields
- `VoiceSelectionsJson` or a separate table if voice selection becomes heavily
  queried.

Indexes:

- `(ChatId, Id)`
- `(ChatId, SortOrder)`
- `(ChatId, Name)` if search/sort needs it
- `(ChatId, ImageId)` for image change/crop notifications

### ChatCharacterRelationships

Break `CharacterRelationshipsJson` into rows.

Columns:

- `ChatId`
- `Id`
- `CharacterAId`
- `CharacterBId`
- `SortOrder`

Detail JSON:

- `DetailsJson`
  - bonds
  - dynamics
  - note A to B
  - note B to A
  - external note

Indexes:

- `(ChatId, CharacterAId)`
- `(ChatId, CharacterBId)`
- `(ChatId, CharacterAId, CharacterBId)`

This makes deleting a character and loading relationship summaries cheaper and
more targeted.

### ChatLocations

Break `LocationsJson` into rows.

Hot/queryable columns:

- `ChatId`
- `Id`
- `Name`
- `ImageId`
- `SortOrder`
- `UpdatedUtc`

Avoid treating `IsActive` as the source of truth if the current scene is
branch-dependent. The active location should come from current scene state or
from `Chats.ActiveLocationId` as a maintained projection.

Cold/detail JSON:

- `DetailsJson`
  - summary
  - description
  - atmosphere
  - features

Indexes:

- `(ChatId, Id)`
- `(ChatId, SortOrder)`
- `(ChatId, ImageId)`

### ChatItems

Break `ItemsJson` into rows.

Hot/queryable columns:

- `ChatId`
- `Id`
- `Name`
- `ImageId`
- `SortOrder`
- `UpdatedUtc`

Scene membership should be branch/current-scene state, not just a static item
field, unless it is explicitly a cached projection.

Cold/detail JSON:

- `DetailsJson`
  - summary
  - description
  - history
  - properties

Indexes:

- `(ChatId, Id)`
- `(ChatId, SortOrder)`
- `(ChatId, ImageId)`

### ChatImages

The current model has both `ImagesJson` and `ImageAssets`. This should be
consolidated so queryable image metadata is not trapped in `ImagesJson`.

Preferred direction:

- Use `ImageAssets` as the durable image metadata table.
- Add any missing gallery/display fields there instead of duplicating in
  `ImagesJson`.
- If a separate gallery ordering table is needed, add `ChatImageGalleryEntries`.

Likely `ImageAssets` additions or confirmed fields:

- `Id`
- `ChatId`
- `Title`
- `EntityType`
- `EntityId`
- `BlobName`
- `StoredContentType`
- `Width`
- `Height`
- `AvatarFocusXPercent`
- `AvatarFocusYPercent`
- `AvatarZoomPercent`
- `CreatedUtc`
- `SortOrder` if gallery ordering is user-controlled

Cold/detail JSON:

- `GenerationMetadataJson`
- prompt/debug fields can stay as text/json because they are not needed for chat
  lists.

Indexes:

- `(ChatId, Id)`
- `(ChatId, EntityType, EntityId)`
- `(ChatId, CreatedUtc)`

Image URL should be derived from image id using one helper/rule, not persisted in
multiple shapes. Crop data belongs to the image row and can be projected into
preview avatars.

### ChatTimelineEntries

Break `TimelineJson` into rows if timeline is edited, searched, filtered, or
shown independently.

Columns:

- `ChatId`
- `Id`
- `SnapshotId`
- `Title`
- `DateText`
- `SortOrder`
- `CreatedUtc`
- `UpdatedUtc`

Detail JSON:

- `DetailsJson`
  - description
  - character names/ids
  - significance
  - any flexible metadata

Indexes:

- `(ChatId, SortOrder)`
- `(ChatId, SnapshotId)`

If timeline becomes tightly coupled to transcript snapshots, revisit this during
the transcript/snapshot overhaul.

### ChatDirection

`ChatDirectionJson` can become a one-to-one table because it is an independently
edited settings surface and also feeds generation.

Possible table:

- `ChatDirectionStates`
  - `ChatId`
  - `SchemaVersion`
  - `Setting`
  - `Premise`
  - `CustomGuidance`
  - `ExplicitContent`
  - `ViolentContent`
  - `TagsJson`
  - `UpdatedUtc`

The list-like fields can stay in `TagsJson` unless they become query filters:

- genres
- tones
- themes
- pacing
- story focus
- boundaries

Reason: this is not hot for global chat listing, but it should not force saving
the whole chat document.

### NarratorProfile

`NarratorProfileJson` can become a one-to-one table.

Columns:

- `ChatId`
- `SchemaVersion`
- `VoicePreset`
- `SetupDepth`
- `VisualDetail`
- `TransitionContext`
- `Foreshadowing`
- `DirectionStrength`
- `CustomGuidance`
- `UpdatedUtc`

JSON or separate table:

- `VoiceSelectionsJson`, or `NarratorVoiceSelections` if this becomes queried or
  shared with voice inventory.

Reason: edited independently, used by generation, not needed for chat list.

### PromptLibrary

`PromptLibraryJson` is mostly configuration/overrides and can remain JSON, but
it should move out of the giant `ChatDocuments` row.

Possible table:

- `ChatPromptLibraryStates`
  - `ChatId`
  - `PromptOverridesJson`
  - `TurnShapeOverridesJson`
  - `UpdatedUtc`

Reason: rarely changed, potentially large, not list-queryable. It just should
not be rewritten when a character name changes or a turn is added.

### CharacterTraitLibrary

`CharacterTraitLibraryJson` is chat-scoped configuration. It can stay JSON in a
dedicated table.

Possible table:

- `ChatCharacterTraitLibraryStates`
  - `ChatId`
  - `SchemaVersion`
  - `LibraryJson`
  - `UpdatedUtc`

Reason: rarely changed, used in character editing, not chat-list queryable.

### ModelTuning

`ModelTuningJson` can stay JSON in a dedicated table.

Possible table:

- `ChatModelTuningStates`
  - `ChatId`
  - `ValuesJson`
  - `UpdatedUtc`

Reason: independently edited settings, not list-queryable.

### StoryAssistant

`StoryAssistantJson` deserves separate treatment. It can grow, it is edited by a
modal/workflow, and it should not be loaded for first paint of an active chat
unless the assistant is visible.

Minimum split:

- `StoryAssistantStates`
  - `ChatId`
  - `SchemaVersion`
  - `ReviewMode`
  - `ActiveAssistantChatId`
  - `UpdatedUtc`
- `StoryAssistantChats`
  - `ChatId`
  - `AssistantChatId`
  - `Title`
  - `CreatedUtc`
  - `UpdatedUtc`
  - `LastResponseId`
  - `ResponseProviderId`
  - `ResponseModelId`
  - `RemoteThreadLost`
  - `RemoteThreadError`
- `StoryAssistantItems`
  - transcript/tool/work item rows, or JSON per assistant chat if we do not need
    fine-grained query/update yet.

This can be deferred, but the important decision is that assistant history
should not live in the primary active chat load path.

## Current Scene Preview Tables

The active/last scene preview should be maintained as derived state.

Possible tables:

- `ChatCurrentScenes`
  - `ChatId`
  - `SourceTurnId`
  - `LocationId`
  - `LocationName`
  - `VisibleTurnCount`
  - `LastGeneratedTurnNumber`
  - `LastMessageUtc`
  - `UpdatedUtc`
- `ChatCurrentSceneCharacters`
  - `ChatId`
  - `CharacterId`
  - `SortOrder`
- `ChatCurrentSceneItems`
  - `ChatId`
  - `ItemId`
  - `SortOrder`

These are projections from the active transcript branch. They should be updated
when:

- the active branch changes
- a turn is added to the active branch
- working/current scene changes
- a character/location/item image changes
- a character/location/item name changes
- image crop changes

The chat list query then joins:

- `Chats`
- `ChatCurrentScenes`
- `ChatLocations`
- `ChatCurrentSceneCharacters`
- `ChatCharacters`
- `ImageAssets`

That produces a renderable `StoryPreview` without hydrating the chat document.

## What Stays JSON

JSON is still useful when the data is:

- rarely queried across chats
- edited as one settings document
- large and detailed
- schema-flexible
- not needed for chat list rendering

Good JSON candidates:

- deep character profile/details
- deep location details
- deep item details
- timeline detail payload, if timeline rows keep searchable columns
- prompt library overrides
- character trait library
- model tuning
- generation metadata
- assistant item details, until assistant needs fine-grained loading

Bad JSON candidates:

- chat list preview data
- character/location/item name
- image id and crop data
- active scene membership
- sort order
- starred state
- last message date
- counts
- fields used in filters/search
- fields updated frequently in isolation

## Loading Strategy

### Chat List

Load `StoryPreview` rows only.

Do not load:

- full characters
- full locations
- items
- prompt library
- assistant history
- transcript details
- inactive branches
- trace/debug payloads

### Active Chat First Paint

Load:

- chat scalar metadata
- current scene preview
- active transcript display path, once transcript overhaul is done
- active scene characters/locations/items needed for the visible UI
- image metadata for referenced avatars
- generation settings needed by visible controls

Lazy/deferred:

- full entity details until entity manager opens or a panel needs them
- Story Assistant history until assistant modal opens
- prompt library until prompt modal opens or generation needs resolved prompts
- detailed traces only when trace UI is expanded
- old/inactive transcript branches only when branch controls/details require them

### Editing Entities

Load and save the relevant table rows, not the whole document.

Examples:

- renaming a character updates `ChatCharacters.Name`
- setting a character image updates `ChatCharacters.ImageId`
- changing crop updates `ImageAssets.AvatarFocusXPercent`,
  `AvatarFocusYPercent`, `AvatarZoomPercent`
- editing character details updates `ChatCharacters.ProfileJson`

Projection updates should be targeted. If a renamed character is in the current
scene, update/invalidate the preview projection; otherwise, chat list preview
does not need to change.

## Write Strategy

Current `ReplaceAreaAsync` makes areas feel separate in memory, but
`SaveChatDocumentAsync` still serializes and writes the whole aggregate. The new
model should persist each area through area-specific commands.

Examples:

- `IStoryPreviewQuery.LoadPreviewsAsync()`
- `ICharacterRepository.SaveCharacterAsync(...)`
- `ILocationRepository.SaveLocationAsync(...)`
- `IImageRepository.SaveCropAsync(...)`
- `IChatDirectionRepository.SaveAsync(...)`
- `IStoryPreviewProjector.UpdateForSceneAsync(...)`

Avoid "save the whole chat" as the default operation. It should be a rare import
or reconstruction path, not normal app behavior.

## UI DRY Contract

Create shared components around the real interaction/data shape:

- `StoryPreviewRow` or `StoryPreviewCard`
- `StoryPreviewAvatarStack`
- `StoryPreviewAvatar`

Both sidebar story list and startup resume cards should use the same underlying
preview model and avatar stack. They may have different layout wrappers, but
they must not independently resolve:

- which characters are visible
- how avatar image/crop data is read
- what fallback avatar looks like
- how the accessible summary is built

If a surface can render without a hydrated chat, every surface should be able to
render without a hydrated chat.

## Migration Direction

Because AgentRp2 is still in design, prefer coherent reconstruction over
compatibility shims.

Suggested implementation order:

1. Define `StoryPreview` read models and shared UI components.
2. Introduce new relational tables for characters, locations, items, images, and
   current scene preview.
3. Build a repository/query path that returns `StoryPreview` without hydrating a
   full chat document.
4. Replace sidebar and startup story rendering with the shared preview model.
5. Move entity editing to targeted row updates.
6. Move settings blobs into dedicated one-to-one tables.
7. Remove `ActiveLocationJson` and `SceneCharactersJson` once preview tables are
   authoritative.
8. Remove `CharactersJson`, `LocationsJson`, `ItemsJson`, `ImagesJson`, and
   similar chat-level blobs once all reads/writes use the new tables.

Do not manually edit project/package metadata while doing this work. Use the
required `dotnet` CLI commands for project/package/reference changes.

## Transcript Placeholder

Transcript deserves a separate detailed pass.

Decisions already discussed:

- Use parent pointers for turns.
- Store active branch as `Chats.ActiveLeafTurnId`.
- Query the active path with a recursive CTE instead of loading all turns.
- Represent snapshots as compressed display nodes over path segments, not as
  story branches.
- Add snapshot jump pointers so a display query can skip covered turn ranges
  instead of loading hundreds of consumed turns.

The transcript design should cover:

- `TranscriptTurns`
- parent/child branch querying
- active path recursive CTE
- sibling branch controls
- snapshot coverage tables
- snapshot jump pointers
- trace/debug payload lazy loading
- active scene projection maintenance

## Core Principle To Preserve

The durable model should make the common operation cheap by default.

For AgentRp2, common operations are:

- list chats
- render current story preview
- open an active chat
- append a turn
- switch a branch
- update scene membership
- rename or re-avatar an entity

Those operations should not deserialize or rewrite unrelated story state. If a
field is frequently queried, frequently changed, or needed before hydration, it
belongs in relational storage or an explicit projection, not hidden inside a
chat-level JSON blob.

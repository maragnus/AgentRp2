# Transcript Overhaul

This document captures the agreed direction for replacing the current
chat-level transcript JSON blob with a relational, branch-aware transcript
model. Reread this document after any context compaction before continuing the
implementation. This is the map; do not make the next person rediscover it like
some kind of productivity tax.

## Why This Exists

The current transcript lives inside `ChatDocuments.MessagesJson` as one large
`RpTranscriptState` blob. That was fast to build, but it creates several
problems:

- Opening a chat can deserialize every turn, inactive branch, snapshot, trace,
  and debug payload.
- A chat with 500+ turns can take minutes to load.
- Saving a new turn or changing branch state rewrites a massive JSON block.
- The active branch is not directly queryable.
- Snapshots collapse UI display but do not currently provide a cheap way to
  skip covered turns at query time.
- Chat list previews depend on derived scene state that should be cheap to read
  and maintain.

The new model makes the active transcript path and current scene preview
first-class query targets.

## Goals

- Cheap listing of all chats with last scene state:
  - location
  - characters
  - avatar image/crop details
  - active branch turn count
- Low overhead maintaining last scene state when it changes.
- Fast active chat load with a smaller memory footprint.
- Efficient branch support without materialized active path rebuilds.
- Minimal write overhead:
  - frequently queried/changed fields become columns
  - bulky rarely queried details remain JSON
- Snapshot-compacted ranges should not require loading every consumed turn.

## Core Decisions

### Transcript Graph

Use `TranscriptTurns` as the graph table.

Each turn has:

- `ChatId`
- `Id`
- `ParentTurnId`

The active branch is stored on `Chats.ActiveLeafTurnId`.

Do not store `IsActive` on turns. Active-ness is contextual and derived by
walking ancestors from the active leaf.

### Active Path Query

Use a recursive CTE from `Chats.ActiveLeafTurnId` back to the root.

Do not maintain a materialized active path table. Branches can change often, so
rebuilding a path table would add write overhead to solve a problem the database
can already handle with parent pointers.

### Snapshots

Snapshots are not branches. They are compressed display nodes covering a
contiguous segment of a branch path.

Use `TranscriptSnapshots` with jump pointers:

- `StartTurnId`
- `EndTurnId`
- `ParentBeforeStartTurnId`
- `TurnNumberStart`
- `TurnNumberEnd`

When traversal reaches a turn id that matches an active snapshot's `EndTurnId`,
the query emits the snapshot and jumps to `ParentBeforeStartTurnId`.

The recursive CTE must be snapshot-aware at the seed and every recursive step.
This is required for a fully compacted transcript where the active leaf is also
the snapshot end.

### Snapshot Coverage

Do not add a snapshot coverage join table for now.

Mark consumed turns directly:

- `ConsumedBySnapshotId`
- `ConsumedBySnapshotOrdinal`

This makes snapshot expansion cheap:

```sql
SELECT *
FROM TranscriptTurns
WHERE ChatId = @chatId
  AND ConsumedBySnapshotId = @snapshotId
ORDER BY ConsumedBySnapshotOrdinal;
```

Snapshots may later consume prior snapshots. Do not over-harden overlap rules in
the database yet. Enforce display and creation rules in application logic.

### Snapshot Access Rules

Once a snapshot is established, normal UI should not allow users to change
branches inside the covered range. If branch editing inside a consumed range is
allowed later, the snapshot must be invalidated, rebuilt, or explicitly forked.

### Message Body

Store `Body` directly on `TranscriptTurns`.

Display queries must be responsible and project only the columns they need.
When loading only path metadata, do not select body.

### Trace Data

Store trace JSON on `TranscriptTurns` for now.

Display/list queries must exclude trace JSON unless the UI explicitly needs it.
Trace is bulky and should not be pulled into first paint by accident.

### Scene Data

Use scene preview columns plus `SceneJson`.

Columns support queryable current scene and display behavior:

- `SceneLocationId`
- `SceneLocationName`

`SceneJson` preserves the full `RpSceneFrame`, including character ids, item ids,
physical states, scene objects, and flexible metadata.

Where practical, prefer columns for frequently queried scene state. Avoid
rewriting unrelated JSON when only list/query fields change.

### Chat Preview Projection

Maintain queryable current scene projection for story listing:

- active location
- active scene characters
- active branch turn count
- last generated turn number
- last message date

The preview projection should be updated when:

- a turn is added to the active branch
- the active branch changes
- scene state changes
- a location/character name or avatar changes
- image crop changes

## Required Tables

### TranscriptTurns

Suggested columns:

- `ChatId`
- `Id`
- `ParentTurnId`
- `TurnNumber`
- `CreatedUtc`
- `UpdatedUtc`
- `Mode`
- `AuthorCharacterId`
- `AuthorName`
- `ActorCharacterId`
- `ActorName`
- `Guidance`
- `Body`
- `SceneLocationId`
- `SceneLocationName`
- `SceneJson`
- `PlanJson`
- `AppearanceJson`
- `PrivateIntentJson`
- `SpeechJson`
- `TraceJson`
- `ConsumedBySnapshotId`
- `ConsumedBySnapshotOrdinal`

Suggested indexes:

- `(ChatId, Id)`
- `(ChatId, ParentTurnId)`
- `(ChatId, ConsumedBySnapshotId, ConsumedBySnapshotOrdinal)`

### TranscriptSnapshots

Suggested columns:

- `ChatId`
- `Id`
- `StartTurnId`
- `EndTurnId`
- `ParentBeforeStartTurnId`
- `TurnNumberStart`
- `TurnNumberEnd`
- `Summary`
- `SceneLocationId`
- `SceneLocationName`
- `SceneJson`
- `SpeechJson`
- `TraceJson`
- `CreatedUtc`
- `UpdatedUtc`
- `ConsumedBySnapshotId`
- `ConsumedBySnapshotOrdinal`
- `IsActive`

Suggested indexes:

- `(ChatId, Id)`
- `(ChatId, EndTurnId, IsActive)`
- `(ChatId, ConsumedBySnapshotId, ConsumedBySnapshotOrdinal)`

### Chats

Add active transcript and preview columns:

- `ActiveLeafTurnId`
- `ActiveTurnCount`
- `LastMessageUtc`
- `LastGeneratedTurnNumber`
- `ActiveLocationId`
- `ActiveLocationName`
- `SnapshotCount` or `HasSnapshots`

`SnapshotCount`/`HasSnapshots` allows the repository to choose a simple
turn-only recursive CTE when no snapshots exist, and the jump-aware CTE when
snapshots exist.

### ChatCurrentSceneCharacters

Suggested columns:

- `ChatId`
- `CharacterId`
- `SortOrder`

This table joins to normalized character/image tables once the broader data
overhaul is implemented. Until then, it can still be maintained as a projection
boundary.

## Display Scenarios The Query Must Handle

### Example 1: Simple

```text
1. message
2. message
3. message
4. message
5. last message
```

No snapshots. Query walks parent pointers from `t5` to `t1`.

### Example 2: Compacted Once

```text
1. snapshot (4 messages)
2. last message
```

Snapshot covers `t1..t4`.

```text
EndTurnId = t4
ParentBeforeStartTurnId = null
```

Traversal starts at `t5`, sees parent/current target `t4` is a snapshot end,
emits snapshot, jumps to null.

### Example 3: Stacked Compact

```text
1. snapshot (2 messages)
2. snapshot (2 messages)
3. last message
```

Snapshots:

```text
s1 covers t1..t2, EndTurnId = t2, ParentBeforeStartTurnId = null
s2 covers t3..t4, EndTurnId = t4, ParentBeforeStartTurnId = t2
```

Traversal from `t5` emits `s2`, jumps to `t2`, emits `s1`, jumps to null.

### Example 4: Center Compact

```text
1. message
2. snapshot (3 messages)
3. last message
```

Snapshot covers `t2..t4`.

```text
EndTurnId = t4
ParentBeforeStartTurnId = t1
```

Traversal emits `t5`, `s1`, `t1`.

### Example 5: Full Compact

```text
1. snapshot (5 messages)
```

Snapshot covers `t1..t5`, and active leaf remains `t5`.

The CTE seed must check whether `t5` is a snapshot end. It emits the snapshot
and jumps to null.

### Example 6: Empty

No active leaf. Query returns no display nodes.

## Query Shape

The query should treat each `CurrentTurnId` as either:

- a snapshot node if it is an active snapshot `EndTurnId`
- a turn node otherwise

Pseudo-SQL:

```sql
WITH DisplayPath AS (
    SELECT
        c.Id AS ChatId,
        c.ActiveLeafTurnId AS CurrentTurnId,
        0 AS Depth
    FROM Chats c
    WHERE c.Id = @chatId
      AND c.ActiveLeafTurnId IS NOT NULL

    UNION ALL

    SELECT
        p.ChatId,
        COALESCE(s.ParentBeforeStartTurnId, t.ParentTurnId) AS CurrentTurnId,
        p.Depth + 1
    FROM DisplayPath p
    LEFT JOIN TranscriptSnapshots s
      ON s.ChatId = p.ChatId
     AND s.EndTurnId = p.CurrentTurnId
     AND s.IsActive = 1
    LEFT JOIN TranscriptTurns t
      ON t.ChatId = p.ChatId
     AND t.Id = p.CurrentTurnId
    WHERE p.CurrentTurnId IS NOT NULL
      AND COALESCE(s.ParentBeforeStartTurnId, t.ParentTurnId) IS NOT NULL
)
SELECT
    p.Depth,
    CASE WHEN s.Id IS NULL THEN 'turn' ELSE 'snapshot' END AS NodeKind,
    COALESCE(s.Id, t.Id) AS NodeId
FROM DisplayPath p
LEFT JOIN TranscriptSnapshots s
  ON s.ChatId = p.ChatId
 AND s.EndTurnId = p.CurrentTurnId
 AND s.IsActive = 1
LEFT JOIN TranscriptTurns t
  ON t.ChatId = p.ChatId
 AND t.Id = p.CurrentTurnId
ORDER BY p.Depth DESC;
```

Implementation can tighten this, split body loading into a second query, or use
a simpler turn-only CTE when `HasSnapshots` is false.

## Implementation Guidance

- Keep UI insulated behind transcript display DTOs.
- Do not return EF row entities directly to display components.
- Prefer query methods such as:
  - `LoadActiveTranscriptDisplayAsync`
  - `LoadSnapshotTranscriptAsync`
  - `LoadTurnSiblingsAsync`
- Initial display queries should not select bulky JSON columns unless required.
- Snapshot detail expansion can query consumed turns by
  `ConsumedBySnapshotId`.
- Branch controls query siblings for visible turns by `ParentTurnId`.
- Current scene projection should be maintained by transcript write operations,
  not reconstructed by the UI.

## Destructive Migration Posture

The user is deleting all chats and starting over. This overhaul does not need to
preserve existing chat data. Prefer the clean schema over compatibility shims.

Do not spend effort migrating old `MessagesJson` data unless explicitly asked.

## Relationship To DATA_OVERHAUL.md

`DATA_OVERHAUL.md` covers the broader move away from chat-level `*Json` blobs.
This document covers transcript-specific branch, snapshot, and display-path
decisions.

Both documents should be kept aligned:

- chat list preview depends on transcript current scene projection
- active branch count comes from transcript path/projection
- entity image/name data should eventually come from normalized non-transcript
  tables

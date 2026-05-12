# Roleplaying Engine

This document describes current engine behavior only.

When changing roleplaying engine behavior, update this document first with the intended new behavior, then update the implementation to match it. Future behavior belongs only in the not-wired section until code starts using it.

## Source Map

| Area | Source |
| --- | --- |
| Turn, CYOA, and snapshot orchestration | [Text generation service](../AgentRp/Services/TextGenerationService.cs) |
| Turn context and prompt tokens | [Transcript prompt context builder](../AgentRp/Services/TranscriptPromptContextBuilder.cs) |
| Snapshot context and prompt tokens | [Snapshot prompt context builder](../AgentRp/Services/TranscriptPromptContextBuilder.Snapshot.cs) |
| Prompt stages and placeholders | [Prompt library service](../AgentRp/Services/PromptLibraryService.cs) |
| Transcript, scene, CYOA, and snapshot models | [Transcript models](../AgentRp/Models/RpTranscriptModels.cs) |
| Transcript commit, branch, and scene operations | [Transcript store](../AgentRp/Session/TranscriptStore.cs) |
| CYOA session flow | [Transcript CYOA store](../AgentRp/Session/TranscriptStore.Cyoa.cs) |
| Snapshot draft and commit flow | [Transcript snapshot store](../AgentRp/Session/TranscriptStore.Snapshots.cs) |

## Core State

| State | Purpose |
| --- | --- |
| `RpTranscriptTurn` | One committed or failed transcript turn. Stores actor, body, plan, appearance map, private intent map, scene frame, trace, and snapshot link. |
| `RpTurnPlan` | Planning output used by prose. Stores turn shape, beat, intent, immediate goal, why-now, change introduced, guardrails, and physical continuity intents. |
| `RpSceneFrame` | Current scene frame. Stores location, in-scene character IDs, in-scene item IDs, character physical states, and scene object states. |
| `RpTranscriptSnapshot` | Compressed transcript baseline. Stores summary, private intent continuity, character appearances, relationship updates, scene, and covered-turn links. |
| `RpCyoaState` | Current CYOA mode, controlled character IDs, pending decision, and autoplay counter. |

## Context Assembly

`TranscriptPromptContextBuilder.BuildTurnContext(...)` builds turn context from the active branch.

Rules:

- The active transcript path is trimmed to the requested parent turn when a parent is supplied.
- The latest snapshot on that path becomes the summary baseline.
- Transcript text contains only turns after the latest snapshot.
- Scene comes from the supplied scene override, or from `TranscriptGraph.GetSceneForNextTurn(...)`.
- Present characters and items come from `RpSceneFrame.InSceneCharacterIds` and `RpSceneFrame.InSceneItemIds`.
- Other known characters are listed by reference only.
- Character appearances start from the latest snapshot, then later turn appearance maps, then explicit overrides.
- Planning and prose receive the planning/prose transcript, which may include private intent lines.
- Selection and Scene Continuity receive the normal transcript, which does not include private intent lines.
- The context does not read story cards.

## Standard Turn Flow

`TextGenerationService.GenerateTurnAsync(...)` handles normal generated turns.

| Order | Step | Current behavior |
| --- | --- | --- |
| 1 | Build context | Builds base context from parent turn, guidance, requested actor/narrator, requested turn shape, and optional scene override. |
| 2 | Capability gate | If structured output is unavailable, skips Scene Continuity, Selection, and Planning. Prose runs from a simple generated plan. A specific actor is required unless narrator was requested. |
| 3 | Scene Continuity | Reconciles appearance, character physical state, and scene object state. |
| 4 | Selection | Runs only when narrator was not requested and no actor was requested. |
| 5 | Rebuild context | Rebuilds context with the continuity scene, appearance overrides, and selected/requested actor. |
| 6 | Planning | Produces structured plan data for the selected actor or narrator. |
| 7 | Prose | Streams the final transcript body from context plus planning output. |
| 8 | Commit | `TranscriptStore.CommitGeneratedTurnAsync(...)` saves actor, body, plan, appearance map, private intent map, scene, and trace. |

## Other Generation Paths

| Path | Flow |
| --- | --- |
| Manual post | Bypasses the model. Saves body, actor/narrator, and next scene frame. |
| Regenerate | Uses the existing plan, appearance map, private intent map, and scene. Runs Prose only. |
| Replan | Uses the existing branch scene and appearance map. Runs Planning, then Prose. It does not run Scene Continuity or Selection. |
| Scene transition | Builds a target scene, then generates a narrator turn with that scene override through the standard turn flow. |

## Step Contracts

### Selection

Source: `RunSelectionStepAsync(...)`.

Runs only during the standard turn flow when no actor is requested and narrator mode is off.

Input context:

- Active speaker name.
- Guidance, if supplied.
- Eligible responder list from present characters.
- Current location.
- Story context and content guidance.
- Recent transcript without private intent lines.
- Current appearance text for present characters.

Rules:

- Present characters are candidates.
- The active speaker is excluded when another present character exists.
- Output is a structured character name.
- The selected name is resolved back to a character ID.
- Selection does not write transcript state.

### Scene Continuity

Source: `RunSceneContinuityStepAsync(...)`.

Runs before actor selection in the standard turn flow and before Planning in selected/autonomous CYOA turns.

Input context:

- Present character list with current appearance state.
- Transcript evidence since the latest snapshot.
- Existing physical scene ledger.
- Continuity intents from prior plans since the latest snapshot.
- Explicit and violent content labels.

Output:

- `AppearanceByCharacterId` for characters with current appearance text.
- Updated `RpSceneFrame.CharacterPhysicalStates` when physical states are returned.
- Updated `RpSceneFrame.SceneObjects` when scene objects are returned.

Rules:

- If physical states or scene objects are omitted, existing scene values are kept.
- Character appearance output is saved on the generated turn.
- Physical states and scene objects are saved inside the generated turn scene.

### Planning

Source: `RunPlanningStepAsync(...)`.

Input context:

- Actor or narrator section.
- Current location.
- Present characters.
- Relevant relationships.
- Other known characters.
- Objects in scene.
- Story context.
- Content guidance.
- History summary.
- Transcript since latest snapshot.
- Current appearance text.
- Physical scene state.
- Continuity plan intents.
- Guidance and requested turn shape.
- Turn-shape definitions and turn-scope rules.

Relationship rules:

- Relationships are included when either side is an in-scene character or the current actor.
- If the actor is in the relationship, the actor-facing private relationship note is used.
- Otherwise the public relationship note is used.

Private intent rules:

- Planning can output one private intent string.
- The private intent is saved only when the actor has a character ID.
- Narrator turns do not save private intent.
- Planning transcript includes prior private intent only for the current actor's own prior turns.
- Narrator planning receives all prior private intent lines.

Output:

- `TurnShape`
- `Beat`
- `Intent`
- `ImmediateGoal`
- `WhyNow`
- `ChangeIntroduced`
- `Guardrails`
- `PrivateIntent`
- `ContinuityIntents`

### Prose

Source: `RunProseStepAsync(...)`.

Input context:

- Same planning/prose context used by Planning.
- Formatted planning output.
- Prose turn-shape instructions.
- Actor or narrator prose rules.
- Audio tag guide.

Rules:

- Prose streams plain transcript body text.
- Prose does not produce structured state.
- The current plan's private intent is included as a prose token.
- The saved private intent still comes from Planning or from the existing plan path, not from prose.
- Character prose is constrained to the current actor.
- Narrator prose is constrained to staging and transition narration.

### Snapshots

Sources: `GenerateSnapshotAsync(...)`, `BuildSnapshotContext(...)`, and snapshot store draft/commit methods.

Flow:

| Order | Step | Current behavior |
| --- | --- | --- |
| 1 | Resolve covered turns | Snapshot draft covers unsnapshotted active-branch turns through the target turn. |
| 2 | Scene Continuity | Runs narrator Scene Continuity for the target turn to refresh appearance and scene state. |
| 3 | Snapshot prompt | Builds snapshot context from covered turns, latest prior snapshot, current scene, characters, locations, items, timeline, relationships, appearance, and physical scene state. |
| 4 | Structured snapshot | Model returns summary, timeline entries, and relationship updates. |
| 5 | Draft | Draft stores summary, covered turns, private intent continuity, character appearances, timeline entries, relationship updates, scene, and trace. |
| 6 | Commit | Commit creates `RpTranscriptSnapshot`, links covered turns, adds timeline entries, and applies selected relationship updates. |

Snapshot context rules:

- Snapshot transcript includes turn labels.
- Snapshot transcript includes all private intent lines from included turns.
- Character details include characters present in final scene, scene physical state, scene objects, authors/actors of covered turns, and characters with private intent in covered turns.
- Location details include the final scene location and locations used by covered turns.
- Snapshot history uses the first three current timeline entries.
- Snapshot timeline entries are generated only during snapshot creation.

Snapshot continuity rules:

- Snapshot private intent continuity is copied from the latest snapshot, then overwritten by covered turn private intent values.
- Snapshot character appearances are copied from the Scene Continuity result when available; otherwise they are copied from the latest snapshot and overwritten by covered turn appearance values.
- Committed snapshot timeline entries are linked to the snapshot ID.
- Relationship updates only apply when the user commits updates with `ApplyChange = true`.

## CYOA Flow

Modes:

| Mode | Behavior |
| --- | --- |
| Off | No CYOA pipeline runs. Normal generation is unchanged. |
| Adventure | Maintains controlled characters. Generates choices when the next actor is controlled or autoplay is exhausted. Otherwise generates autonomous turns. |
| Director | Generates choices from narrator/director mode. The prompt asks options to target narrator or one present character. |

Directions:

| Direction | Current meaning |
| --- | --- |
| Continue | Keep the current path moving. |
| Escalate | Increase pressure, stakes, intimacy, conflict, urgency, or consequence. |
| Pivot | Redirect topic, focus, tactic, attention, or emotional angle. |
| Fast Forward | Suggest a time skip, wait, location change, or scene reset for user approval. |

Adventure pipeline:

- Controlled character IDs are normalized to existing characters.
- If Adventure has no controlled characters, the mode turns off.
- Actor selection considers present characters.
- When autoplay is exhausted, actor selection is forced to controlled characters.
- If the selected actor is controlled, the app generates a pending choice set.
- If the selected actor is not controlled, the app generates an autonomous turn and decrements autoplay.
- Autoplay resets to `RpCyoaState.MaxAutoplayTurns` when the app reaches a controlled choice.

Choice generation:

- Requires structured output.
- Builds Planning-stage context.
- Generates exactly one option for Continue, Escalate, Pivot, and Fast Forward.
- Adventure options are for the selected controlled actor.
- The Director prompt asks options to target narrator or one present character. Code resolves returned actor names against the character catalog and falls back to narrator when no character resolves.
- Fast Forward options include a scene proposal.
- Choice options can store private intent, but selected-turn persistence does not directly commit that option private intent.

Selected choice:

- Selecting Continue, Escalate, or Pivot consumes the pending decision.
- Custom guidance also consumes the pending decision and runs the selected CYOA turn path without an option.
- Selected CYOA turns run Scene Continuity, Planning, and Prose.
- The option plan is used as planning guidance, not as the committed plan.
- Persisted private intent comes from the selected turn Planning step.

Fast Forward:

- Selecting a Fast Forward option opens a review state instead of writing prose immediately.
- Applying Fast Forward builds a scene transition request from the proposal.
- The scene transition writes a narrator turn through the standard scene-transition generation path.
- Canceling Fast Forward clears only the review state.

Autonomous CYOA turns:

- Run when Adventure selects a non-controlled actor.
- Use Scene Continuity, an internal direction-choice prompt, Planning, and Prose.
- The internal direction choice uses Planning tuning but is not a prompt-library stage.
- Autonomous Fast Forward is converted to Pivot before planning, so autonomous turns do not execute a time skip or scene change.

## Element Rules

| Element | Selection | Scene Continuity | Planning | Prose | Snapshot |
| --- | --- | --- | --- | --- | --- |
| Actor/narrator | Selects actor only when no actor is requested. | Does not choose actor. | Plans for selected/requested actor or narrator. | Writes only selected/requested actor or narrator. | No actor selection. |
| Present characters | Candidate pool and appearance summaries. | Appearance and physical state subjects. | Full character details for in-scene characters. | Same planning/prose context. | Details for referenced/final-scene characters. |
| Other known characters | Not included except story context may reference them indirectly. | Not included. | Listed by name/id/pronouns only. | Same planning/prose context. | Full character catalog plus details for referenced characters. |
| Relationships | Not included. | Not included. | Included when connected to actor or present character; actor-facing note when actor is involved. | Same planning/prose context. | Current relationships are included for refresh; committed updates may apply relationship changes. |
| Location | Current location block. | Current scene evidence. | Current location details. | Same planning/prose context. | Current location plus location catalog/details for covered turns. |
| Items and scene objects | Present item names are not part of selection-specific tokens. | May update scene object ledger. | Present items and scene object ledger are included. | Same planning/prose context. | Item catalog and object/scene state evidence. |
| Transcript | Recent transcript since latest snapshot, no private intent. | Transcript evidence since latest snapshot, no private intent. | Transcript since latest snapshot, with allowed private intent lines. | Same planning/prose transcript. | Covered turns with turn labels and all private intent lines. |
| Private intent | Not included and not produced. | Not included and not produced. | May read allowed prior private intent; may produce current actor private intent. | Receives current plan private intent as input only. | Carries forward latest snapshot private intent and covered turn private intent. |
| Appearance | Reads current appearance text. | Produces appearance map. | Reads current appearance text including continuity output. | Same planning/prose context. | Stores refreshed appearance map or accumulated turn appearance maps. |
| Physical scene state | Not included beyond appearance text. | Reads and may replace physical scene state. | Reads physical scene state. | Same planning/prose context. | Stores final scene and uses physical scene state as snapshot evidence. |
| Continuity intents | Not included. | Reads prior plan continuity intents since latest snapshot. | Can produce new continuity intents. | Receives formatted planning output with continuity intents. | Not produced directly; covered plans can affect snapshot evidence. |
| Timeline/history | Story context and content guidance only. | Not included except transcript evidence. | Full timeline in story context and up to eight-item history summary. | Same planning/prose context. | Reads current timeline; may create new timeline entries on commit. |
| Guidance/turn shape | Guidance can affect responder selection. | Does not use turn shape. | Uses guidance and requested turn shape. | Uses guidance and resolved plan turn shape. | Not turn-shaped. |

## Not Currently Wired

Story Cards are persisted, editable, and can be attached to a story, but they do not currently enter the generation engine context.

Role, Item, Location, Phase, phase transition, and phase requirement rules are therefore not applied by Selection, Scene Continuity, Planning, Prose, Snapshot, or CYOA generation.

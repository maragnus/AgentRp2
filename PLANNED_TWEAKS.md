# Planned Tweaks

This is a list of future tweaks that are planned. Be sure to remove these as they are implemented.

## Low hanging fruit

- LiveRoleplayStore is utter shit and needs to use SemaphoreSlim, ConcurrentDictionary, DistributedCache
- All text fields should send an update after a delay of 2 seconds after being changed.
  Essentially, all text fields should be `TextUpdateMode.Change` by default, but should not update while actively typing, only after being unchanged for 2 seconds (reuse changeDebounceMilliseconds).

## Story Cards
- The story premise should be like the TV series premise that is consistent through all seasons and episodes
- A story card should be either a season-scope (long running) or an episode-scope (short running)

Story Cards catalog modal is doing double duty and it needs to be split into two separate modals:
- Catalog: List story cards
- Editor: Edit a single story card

Catalog should be exactly that: a catalog of cards
- optional cover image
- title
- summary
- number of phases, roles, items, locations
- buttons to apply to current story, edit, and remix 
This story card view should be DRY and reusable to also list in instances in the Story Direction modal Cards tab.
- buttons to manage, pause/resume the instance
- manage button opens a dedicate modal to manage the card as part of the story.

Editor should be the home of all of the editor bits.
- Dedicated Story Card editor modal that entirely focuses on a single story card. 
- No sidebar to list other story cards.

# Introduce credits
- Credits are user-based
- Credit transaction list: story, entity type, model, usage level, action, timestamp, cost, etc
- Credit usage breakdown by story, modality, model, action, day
- Chat messages, images, snapshots, story assistant, tts usage all link to a credit transaction
- Credit transactions cannot be deleted and persist if their entity is deleted
- Model usage calls cost credits, typically static credits but can be variable by usage level. Model+Usage Level=credit cost
- Image generation may include usage levels based on output quality selected or use of reference images
- Weird rules: generating snapshot draft is free, discarding the draft costs 5 credits, keeping the snapshot awards 5 credits
- AI Providers modal can assign credit cost per model and usage level
- New actions should be blocked if at 0 or no credits. But a running action should allow the user to enter negative balances. Example, user is at 1 credit and sends a high usage message and TTS is enabled, the user should get the full experience but be at -5 credits blocking them from further actions.
- Exception: messages are free under the threshold in the transcript, but if TTS is enabled, the user should be told to disable it, otherwise they can't send the message.

Examples:
Chat Messages < 25 in transcript = free (low usage)
Chat Messages 25 - 35 in transcript = 1 credit (medium usage)
Chat Messages >= 35 in transcript = 4 credit (high usage)
Image is quality based = 5, 8, 15 credits for low, medium, high
TTS: short = 1 credit, long = 2 credit 


# Narrator event-based insertion 

After scene change
After time skip
When location changes
When pacing stalls
At chapter/act boundaries
Manually only
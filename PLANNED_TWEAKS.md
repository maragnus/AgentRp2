# Planned Tweaks

This is a list of future tweaks that are planned. Be sure to remove these as they are implemented.

## Low hanging fruit

- none

## Story Cards
- Introduce "Story cards" Story Entities
- Replaces the story hook to guide the story.

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
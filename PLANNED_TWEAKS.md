# Planned Tweaks

This is a list of future tweaks that are planned. Be sure to remove these as they are implemented.

## Low hanging fruit

- Creating a snapshot should automatically exclude the last message in the transcript. There needs to always be at least one message after the transcript to continue the story consistently.

- Timeline editor:
    - Timeline header should use the overlapping avatar list that Sidebar Story List uses.
    - Title and Date should be on the same row, Title should be 2/3 width.
    - Story Context Characters should be a multi-select of characters that shows their avatar and name. (we may need a new component for this?)



## Story Cards
- Introduce "Story cards" Story Entities
- Replaces the story hook to guide the story.

# Narrator event-based insertion 

After scene change
After time skip
When location changes
When pacing stalls
At chapter/act boundaries
Manually only

# Users

We need a concept of a user.
- User table with GUID id
- User-based: Stories, Images, Preferences
- System globals: AI Providers, selected Models, Model Tuning, Prompt Library
- User roles
    - Admins manage system globals
    - Super Users can access Process block details (see prompts)

Accessing system and user prompts is restricted to Admins and Super Users. Accessing model outputs is accessible to all users.

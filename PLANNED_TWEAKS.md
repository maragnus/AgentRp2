# Planned Tweaks

This is a list of future tweaks that are planned. Be sure to remove these as they are implemented.

## Story Cards
- Introduce "Story cards" Story Entities
- Replaces the story hook to guide the story.


## Choose your own adventure (CYOA) modes:

- Different CYOA modes select which characters are "controlled".
  - Character control mode
  - Narrator control mode

NOTE: We need better names for the modes. It's also a very different experience to control one character versus all or most of them. Making Character control mode more "adventure vs guided story" or something.

- All "controlled" characters will provide multiple plan options at the start of their turn instead of just one. The user can choose one plan to guide interactions, users can also provide their own written guidance which creates a plan based off it. After a plan is selected, it's applied and continues to prose as normal.
    - Users can chose which characters are controlled

- Character mode: Only "controlled" character offers plan options after every turn by another haracter, other characters are fully autonomous.
    - Plan to continue the current narrative path
    - Plan to escalate the current narrative path
    - Plan to make a direction change (change topic or something interesting to freshen up the scene)
    - Plan a fast forward (using the narrator via exising SetScene) to skip some time or move to another scene
    - User can write custom guidance to generate and execute a plan
    - Skip and let selection pick another character to go

- Narrator mode: User acts as the narrator, in a sense, and guides the story itself, not individual characters. 
    - Narrator provides suggested paths the story can take
    - Narrator can speak as itself, guide specific characters to act over one to a few turns

We need to add inline option selection UI/UX in the Chat Transcript. In any CYOA mode, the traditional footer should be hidden.

---

To make this work we also need to introduce a story orchestration layer for CYOA that uses the smarter "reasoning" model. It needs to provide 

---

# Narrator event-based insertion 

After scene change
After time skip
When location changes
When pacing stalls
At chapter/act boundaries
Manually only

using AgentRp.Session;

namespace AgentRp.Models;

public static class SeedData
{
    public static List<RpCharacter> Characters() =>
    [
        new() { Id = "c1", Name = "Bella", ImageId = "g7", InScene = true, Summary = "A warm, accomplished surgeon who brings steadiness and affection to the tense dynamic.", Personality = "Sweet, emotionally intelligent, remarkably steady under pressure. Balances clinical composure with genuine warmth.", Appearance = "Short brunette around 5'4\". Approachable presence, quietly confident, polished air softened by warmth.", Backstory = "Completed her surgical residency last year. Moved in with Jake six months ago.", Voice = "Measured and warm. Asks before telling. Touches people she cares about.", SceneRoles = ["anchor", "mediator"], Traits = ["open-hearted", "caretaker", "strategic-de-escalator"], CoreDrive = "protect-their-people", CoreFear = "hurting-others", SurfaceMask = "helpful-capable", HiddenTruth = "needs-reassurance", SentenceStyle = "precise", HonestyStyle = "direct", EmotionalLeakage = "gets-warmer", ActionFingerprint = "touch-connector", StressPattern = "helpful-under-pressure", SoftSpots = ["practical-care", "being-trusted"], AvoidPatterns = ["no-solve-every-conflict", "no-instant-vulnerable"], Drives = ["protect-their-people", "keep-peace"], Limits = ["No instant confessions", "No solving every conflict"] },
        new() { Id = "c2", Name = "Gemma", ImageId = "g2", InScene = true, Summary = "A striking, sharp-mouthed woman who masks deep vulnerability with bold confidence.", Personality = "Sharp-tongued, quick-witted, magnetic. Uses humor and provocation as defense mechanisms. Fiercely loyal once trust is earned.", Appearance = "Tall blonde, model's posture. Wardrobe that never lets anyone forget she knows it.", Backstory = "Works in brand consulting. Has lived in the Devonshire with Jake for two years.", Voice = "Fast, dry, a bit cutting. Switches to soft when caught off guard.", SceneRoles = ["complication", "button-pusher"], Traits = ["deadpan-deflector", "snarky", "guarded"], CoreDrive = "be-wanted", CoreFear = "being-rejected", SurfaceMask = "smug-untouchable", HiddenTruth = "wants-to-be-chosen", SentenceStyle = "terse", HonestyStyle = "layered", EmotionalLeakage = "gets-sharper", ActionFingerprint = "lounger", StressPattern = "funnier-under-pressure", SoftSpots = ["quiet-inclusion"], AvoidPatterns = ["no-random-cruelty", "no-instant-vulnerable"], Drives = ["be-wanted", "preserve-independence"], Limits = ["No random cruelty", "No instant vulnerability"] },
        new() { Id = "c3", Name = "Jake", ImageId = "g4", InScene = true, Summary = "Gemma's polite, work-from-home roommate with a quiet intensity.", Personality = "Reserved and thoughtful. Observes more than he speaks. Dry wit surfaces rarely. Internally more complicated than he presents.", Appearance = "6'1\", athletic. Usually in casual clothes. Clean-cut with a slightly guarded expression.", Backstory = "Remote software architect. Moved to the Devonshire two years ago. Met Gemma through mutual friends.", Voice = "Measured, few words. When he does speak it lands.", Traits = ["controlled", "observer", "dry-wit"], Drives = ["maintain-control"], Limits = ["No sudden speeches"] },
        new() { Id = "c4", Name = "Tyler", ImageId = "g3", InScene = false, Summary = "Gemma's on-again-off-again friend who drifts in and out of the scene.", Personality = "Easygoing and charming on the surface, harder to read underneath.", Appearance = "Broad-shouldered, sandy hair. Always looks like he just got back from somewhere interesting." }
    ];

    public static List<RpCharacterRelationship> CharacterRelationships() =>
    [
        new() { Id = "relationship-c1-c2", CharacterAId = "c1", CharacterBId = "c2", NoteAtoB = "Bella sees Gemma as her best friend, sharp-edged but deeply trusted.", NoteBtoA = "Gemma trusts Bella more than she admits and softens around her.", NoteExternal = "Best friends with charged trust.", Bonds = ["Ally"], Dynamics = ["Protective"] },
        new() { Id = "relationship-c1-c3", CharacterAId = "c1", CharacterBId = "c3", NoteAtoB = "Bella loves Jake and looks for the steadiness beneath his guarded silences.", NoteBtoA = "Jake sees Bella as his girlfriend and emotional anchor.", NoteExternal = "Committed partners under pressure.", Bonds = ["Romantic Interest"], Dynamics = ["Protective"] },
        new() { Id = "relationship-c2-c3", CharacterAId = "c2", CharacterBId = "c3", NoteAtoB = "Gemma needles Jake because the unresolved history still matters to her.", NoteBtoA = "Jake keeps Gemma at a careful distance, aware of the tension between them.", NoteExternal = "Roommates with unresolved tension.", Bonds = ["Complicated"], Dynamics = ["Unspoken tension"] },
        new() { Id = "relationship-c2-c4", CharacterAId = "c2", CharacterBId = "c4", NoteAtoB = "Gemma treats Tyler as a complicated friend who drifts in and out.", NoteBtoA = "Tyler reads Gemma as magnetic and difficult to pin down.", NoteExternal = "On-again-off-again friendship.", Bonds = ["Close Friend"], Dynamics = ["Unspoken tension"] },
        new() { Id = "relationship-c3-c4", CharacterAId = "c3", CharacterBId = "c4", NoteAtoB = "Jake knows Tyler casually and keeps the connection low-stakes.", NoteBtoA = "Tyler treats Jake as a casual acquaintance.", NoteExternal = "Casual acquaintances.", Bonds = ["Acquaintance"], Dynamics = [] }
    ];

    public static List<RpLocation> Locations() =>
    [
        new() { Id = "l1", Name = "Devonshire Apartment 822", ImageId = "g1", IsActive = true, Summary = "Main gathering space. Well-appointed open-plan apartment in the Devonshire building.", Description = "Open-plan living space with modern furnishings and floor-to-ceiling windows.", Atmosphere = "Charged and comfortable. Claustrophobic when tensions run high.", Features = "- Open living area with dining table\n- Kitchen with island\n- Two bedrooms (Jake, Gemma)\n- Balcony" },
        new() { Id = "l2", Name = "City Park", Summary = "The park across from the Devonshire. Open, neutral ground.", Description = "A leafy urban park with benches and winding paths.", Atmosphere = "Neutral, open, relieving.", Features = "- Benches\n- Winding pathway\n- Trees providing shade" }
    ];

    public static List<RpItem> Items() =>
    [
        new() { Id = "i1", Name = "Tesla Model S Plaid", ImageId = "g8", Summary = "Jake's car. Silver, sleek, impractical for the city.", Description = "A modern silver electric sedan. Clean interior, dark tints.", History = "Jake bought it two years ago. Gemma borrowed it once and still owes him the charging fee.", Properties = "Color: Silver\nModel: Tesla Model S Plaid\nLocation: Street outside Devonshire" }
    ];

    public static List<RpTimelineEntry> Timeline() =>
    [
        new() { Id = "t1", Title = "Bella arrives at the apartment", Date = "Apr 26, 2026", Description = "Bella lets herself in and finds Jake and Gemma in a tense standoff at the dining table.", Characters = ["Bella", "Jake", "Gemma"], Significance = "Opens Act 1 of the Devonshire Games arc." },
        new() { Id = "t2", Title = "Jake and Gemma move in together", Date = "Two years ago", Description = "Jake and Gemma become roommates at the Devonshire through mutual friends.", Characters = ["Jake", "Gemma"], Significance = "Establishes the baseline tension." }
    ];

    public static List<GalleryImage> GalleryImages() =>
    [
        new() { Id = "g1", Name = "Devonshire Apt 822", Entity = "Devonshire Apartment 822", EntityType = "location", Date = "Apr 25", Hue = 210 },
        new() { Id = "g2", Name = "Gemma", Entity = "Gemma", EntityType = "character", Date = "Apr 25", Hue = 15 },
        new() { Id = "g3", Name = "Tyler", Entity = "Tyler", EntityType = "character", Date = "Apr 24", Hue = 160 },
        new() { Id = "g4", Name = "Jake (desk)", Entity = "Jake", EntityType = "character", Date = "Apr 24", Hue = 245 },
        new() { Id = "g5", Name = "Jake (standing)", Entity = "Jake", EntityType = "character", Date = "Apr 24", Hue = 245 },
        new() { Id = "g6", Name = "Gemma (glam)", Entity = "Gemma", EntityType = "character", Date = "Apr 24", Hue = 15 },
        new() { Id = "g7", Name = "Bella", Entity = "Bella", EntityType = "character", Date = "Apr 24", Hue = 68 },
        new() { Id = "g8", Name = "Tesla Model S Plaid", Entity = "Tesla Model S Plaid", EntityType = "item", Date = "Apr 25", Hue = 200 }
    ];

    public static List<RpChat> Chats() =>
    [
        new() { Id = "ch1", Title = "Devonshire Games", Updated = "Apr 26", Starred = true, Messages = 4, Location = "Devonshire Apartment 822" },
        new() { Id = "ch2", Title = "Park Encounter", Updated = "Apr 23", Messages = 8, Location = "City Park" },
        new() { Id = "ch3", Title = "Morning After", Updated = "Apr 21", Starred = true, Messages = 12, Location = "Devonshire Apartment 822" },
        new() { Id = "ch4", Title = "The Drive", Updated = "Apr 19", Messages = 3, Location = "Tesla Model S Plaid" }
    ];

    public static RpTranscriptState Transcript()
    {
        var started = DateTime.UtcNow.Date.AddDays(-1).AddHours(18);
        var rootScene = new RpSceneFrame
        {
            LocationId = "l1",
            LocationName = "Devonshire Apartment 822",
            InSceneCharacterIds = ["c1", "c2", "c3"]
        };

        var turn1 = new RpTranscriptTurn
        {
            Id = "turn-1",
            CreatedUtc = started,
            UpdatedUtc = started,
            Mode = "guided",
            AuthorName = "Narrator",
            ActorName = "Narrator",
            Body = "Bella knocks once and lets herself into the apartment's open living space, her gaze sweeping the charged silence at the table where Jake sits rigidly in sweats and hoodie, arms crossed in obstinate simmer, while Gemma lounges opposite in a loose crop top and miniskirt hiked daringly high, her deadpan expression a mask of defiant nonchalance; the roommates' mismatched energies hang thick, postures locked in pre-arrival friction, as Bella's warm eyes linger a beat too long on Gemma's revealing hem, a subtle thrill flickering beneath her composed smile.",
            Plan = new()
            {
                TurnShape = "Brief",
                Beat = "Arrival and de-escalation attempt.",
                Intent = "Set the opening tension and invite the next beat through Bella's entrance.",
                ImmediateGoal = "Bring Bella into the room as the new dramatic variable.",
                WhyNow = "Her arrival interrupts a fragile two-person standoff.",
                ChangeIntroduced = "The scene becomes a three-way emotional triangle.",
                Guardrails = "Keep the setup grounded in visible action."
            },
            AppearanceByCharacterId = new()
            {
                ["c1"] = "Standing just inside the apartment entrance. Coat still on. Gaze moving across the room toward the table. Expression: composed with a slight cautious smile.",
                ["c2"] = "Seated at the dining table. Posture rigid, arms crossed. Wearing a loose crop top and miniskirt hiked high. Expression: deadpan, gaze flat.",
                ["c3"] = "Seated at the dining table. Posture rigid, arms crossed. Wearing grey sweats. Expression: tense and guarded."
            },
            PrivateIntentByCharacterId = new() { ["c1"] = "Take in the tension before choosing how to intervene." },
            SnapshotId = "snap-1",
            Trace = GuidedNarratorTrace(started),
            Scene = SessionCloner.Clone(rootScene)
        };

        var turn2Time = started.AddMinutes(3);
        var scene2 = SessionCloner.Clone(rootScene);
        var turn2 = new RpTranscriptTurn
        {
            Id = "turn-2",
            ParentTurnId = "turn-1",
            CreatedUtc = turn2Time,
            UpdatedUtc = turn2Time,
            Mode = "automatic",
            AuthorCharacterId = "c1",
            AuthorName = "Bella",
            ActorCharacterId = "c1",
            ActorName = "Bella",
            Body = "Bella crosses to the table, rests a gentle hand on Jake's shoulder then Gemma's bare midriff. \"Hey, you two... missed you both.\" *smiles warmly*",
            Guidance = "",
            Plan = new()
            {
                TurnShape = "Brief",
                Beat = "Arrival and de-escalation attempt.",
                Intent = "Acknowledge both people without choosing sides.",
                ImmediateGoal = "Claim her place in the room with gentle physical contact.",
                WhyNow = "Waiting would let the tension calcify.",
                ChangeIntroduced = "Bella becomes the active bridge between Jake and Gemma.",
                Guardrails = "Keep the touch warm and unforced."
            },
            AppearanceByCharacterId = new()
            {
                ["c1"] = "Standing at the dining table between Jake and Gemma, one hand on Jake's shoulder and one on Gemma's bare midriff.",
                ["c2"] = "Still seated at the table, gaze angled up toward Bella.",
                ["c3"] = "Still seated, tense, attention pulled to Bella's touch."
            },
            PrivateIntentByCharacterId = new() { ["c1"] = "Ground both of them without naming the conflict outright." },
            SnapshotId = "snap-1",
            Trace = BellaTrace(turn2Time),
            Scene = scene2
        };

        var turn3Time = turn2Time.AddMinutes(2);
        var turn3 = new RpTranscriptTurn
        {
            Id = "turn-3",
            ParentTurnId = "turn-2",
            CreatedUtc = turn3Time,
            UpdatedUtc = turn3Time,
            Mode = "automatic",
            AuthorCharacterId = "c2",
            AuthorName = "Gemma",
            ActorCharacterId = "c2",
            ActorName = "Gemma",
            Body = "Gemma tips her chin up toward Bella without turning, a slow half-smile pulling at the corner of her mouth. \"Missed you too, Bell.\" *her gaze slides briefly to Jake* \"Glad someone did.\"",
            Plan = new()
            {
                TurnShape = "Brief",
                Beat = "Deflection through warmth.",
                Intent = "Accept Bella's warmth without looking exposed.",
                ImmediateGoal = "Return the affection and land a plausible barb at Jake.",
                WhyNow = "Bella has created a tiny opening, and Gemma wants to exploit it before it closes.",
                ChangeIntroduced = "Gemma redirects the tension toward Jake without rejecting Bella.",
                Guardrails = "Keep the barb plausible and emotionally revealing."
            },
            AppearanceByCharacterId = new()
            {
                ["c1"] = "Still standing between them, attention fixed on Gemma's reply.",
                ["c2"] = "Chin tipped up toward Bella, half-smile visible, gaze briefly sliding to Jake.",
                ["c3"] = "Seated and quiet, his focus caught between Bella's touch and Gemma's barb."
            },
            PrivateIntentByCharacterId = new() { ["c2"] = "Take Bella's affection while reminding Jake she noticed his silence." },
            Trace = GemmaTrace(turn3Time),
            Scene = SessionCloner.Clone(scene2)
        };

        var snapshot = new RpTranscriptSnapshot
        {
            Id = "snap-1",
            TurnId = "turn-2",
            CreatedUtc = turn2Time,
            Summary = "Bella has entered the apartment, crossed to the table, and physically bridged Jake and Gemma without taking sides, shifting the scene from a standoff to a tense triangle.",
            PrivateIntentByCharacterId = new()
            {
                ["c1"] = "Bella wants to ground both of them without forcing the conflict into the open."
            },
            CharacterAppearances = new()
            {
                ["c1"] = turn2.AppearanceByCharacterId["c1"],
                ["c2"] = turn2.AppearanceByCharacterId["c2"],
                ["c3"] = turn2.AppearanceByCharacterId["c3"]
            },
            Scene = SessionCloner.Clone(scene2),
            Trace = SnapshotTrace(turn2Time.AddSeconds(2))
        };

        return new()
        {
            RootScene = rootScene,
            Turns = [turn1, turn2, turn3],
            Snapshots = [snapshot],
            ActiveLeafTurnId = "turn-3",
            BranchSelections = new()
            {
                [TranscriptGraph.RootBranchKey] = "turn-1",
                ["turn-1"] = "turn-2",
                ["turn-2"] = "turn-3"
            }
        };
    }

    public static List<AiProvider> Providers() =>
    [
        new()
        {
            Id = "ap1", Type = "grok", Name = "Grok / xAI", Enabled = true,
            ApiKey = "xai-••••••••••••••••••••••••••••••••",
            Models =
            [
                new() { Id = "grok-4-1-fast-non-reasoning", DisplayName = "grok-4-1-fast-non-reasoning", CreatedUnix = 1773014400, Enabled = true, Roles = [AiModelRole.Chat] },
                new() { Id = "grok-4-0709", CreatedUnix = 1752019200, Enabled = true, Roles = [AiModelRole.Chat] },
                new() { Id = "grok-4.20-0309", CreatedUnix = 1776384000, Enabled = true, Roles = [AiModelRole.Chat, AiModelRole.Image] },
                new() { Id = "grok-4.20-0309-non-reasoning", CreatedUnix = 1776384000, Roles = [AiModelRole.Chat] },
                new() { Id = "grok-4.20-0309-reasoning", CreatedUnix = 1776384000, Roles = [AiModelRole.Chat] },
                new() { Id = "grok-vision-beta", Roles = [AiModelRole.Chat, AiModelRole.Image] },
                new() { Id = "xai-tts", Enabled = true, Roles = [AiModelRole.Voice] }
            ]
        },
        new()
        {
            Id = "ap3", Type = "openai", Name = "OpenAI", Enabled = true,
            ApiKey = "sk-••••••••••••••••••••••••••••••••",
            Models =
            [
                new() { Id = "gpt-4o", Enabled = true, Roles = [AiModelRole.Chat, AiModelRole.Image] },
                new() { Id = "gpt-4o-mini", Enabled = true, Roles = [AiModelRole.Chat, AiModelRole.Image] },
                new() { Id = "o3", Enabled = true, Roles = [AiModelRole.Chat] },
                new() { Id = "o4-mini", Roles = [AiModelRole.Chat] },
                new() { Id = "gpt-4-turbo", Roles = [AiModelRole.Chat, AiModelRole.Image] },
                new() { Id = "gpt-3.5-turbo", Roles = [AiModelRole.Chat] },
                new() { Id = "dall-e-3", Roles = [AiModelRole.Image] },
                new() { Id = "gpt-4o-mini-tts", Enabled = true, Roles = [AiModelRole.Voice] }
            ]
        }
    ];

    public static IReadOnlyList<AiProviderMeta> ProviderMetadata() =>
    [
        new()
        {
            Id = "openai",
            Name = "OpenAI",
            Description = "OpenAI models for text, image, and speech generation.",
            KeyLabel = "OpenAI API Key",
            KeyLink = "https://platform.openai.com/api-keys",
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "gpt-image-2", Roles = [AiModelRole.Image] },
                new() { Id = "gpt-image-1.5", Roles = [AiModelRole.Image] },
                new() { Id = "gpt-image-1", Roles = [AiModelRole.Image] },
                new() { Id = "gpt-image-1-mini", Roles = [AiModelRole.Image] },
                new() { Id = "gpt-5.5", Roles = [AiModelRole.Chat] },
                new() { Id = "gpt-5.5-mini", Roles = [AiModelRole.Chat] },
                new() { Id = "gpt-4o-mini-tts", Roles = [AiModelRole.Voice] }
            ]
        },
        new()
        {
            Id = "grok",
            Name = "Grok / xAI",
            Description = "xAI Open Responses-compatible Grok and voice models.",
            KeyLabel = "xAI API Key",
            KeyLink = "https://console.x.ai",
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "grok-4-1-fast-non-reasoning", Roles = [AiModelRole.Chat] },
                new() { Id = "grok-4-0709", Roles = [AiModelRole.Chat] },
                new() { Id = "grok-imagine-image", Roles = [AiModelRole.Image] },
                new() { Id = "grok-4.20-0309", Roles = [AiModelRole.Chat] },
                new() { Id = "grok-4.20-0309-non-reasoning", Roles = [AiModelRole.Chat] },
                new() { Id = "grok-4.20-0309-reasoning", Roles = [AiModelRole.Chat] },
                new() { Id = "xai-tts", Roles = [AiModelRole.Voice] }
            ]
        },
        new()
        {
            Id = "claude",
            Name = "Claude / Anthropic",
            Description = "Claude through Anthropic's /v1 endpoint.",
            KeyLabel = "Anthropic API Key",
            KeyLink = "https://console.anthropic.com/settings/keys",
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "claude-opus-4-5", Roles = [AiModelRole.Chat] },
                new() { Id = "claude-sonnet-4-5", Roles = [AiModelRole.Chat] },
                new() { Id = "claude-haiku-4-5", Roles = [AiModelRole.Chat] },
                new() { Id = "claude-3-5-sonnet-20241022", Roles = [AiModelRole.Chat] },
                new() { Id = "claude-3-haiku-20240307", Roles = [AiModelRole.Chat] }
            ]
        },
        new()
        {
            Id = "elevenlabs",
            Name = "ElevenLabs",
            Description = "ElevenLabs text-to-speech models and voice library.",
            KeyLabel = "ElevenLabs API Key",
            KeyLink = "https://elevenlabs.io/app/settings/api-keys",
            ApiKeyRequired = true
        },
        new()
        {
            Id = "huggingface",
            Name = "Hugging Face",
            Description = "Managed Hugging Face endpoints exposed through a Responses/Open Responses-compatible /v1 endpoint.",
            KeyLabel = "Hugging Face API Key",
            KeyLink = "https://huggingface.co/settings/tokens",
            NeedsEndpoint = false,
            ApiKeyRequired = true
        },
        new()
        {
            Id = "compatible",
            Name = "OpenAI-compatible",
            Description = "Any Responses/Open Responses-compatible gateway.",
            KeyLabel = "API Key",
            NeedsEndpoint = true,
            EndpointRequired = true
        }
    ];

    public static IReadOnlyList<TaxonomyGroup> Taxonomy() =>
    [
        new("Scene role", "amber", ["Instigator", "Anchor", "Mirror", "Complication", "Conscience", "Witness"]),
        new("Conflict", "rose", ["Deadpan Deflector", "Bratty Provoker", "Boundary Setter", "Strategic De-escalator"]),
        new("Emotional style", "violet", ["Open-hearted", "Guarded", "Volatile", "Controlled", "Numb"]),
        new("Social style", "blue", ["Charmer", "Caretaker", "Observer", "Social Chameleon", "Outsider"]),
        new("Limits", "emerald", ["No Random Cruelty", "No Instant Vulnerability", "No Psychic Knowledge", "No Solving Every Conflict"])
    ];

    static RpTurnTrace GuidedNarratorTrace(DateTime startedUtc) => new()
    {
        Summary = "Completed · Narrator · Appearance -> Planning -> Prose",
        Status = "completed",
        StartedUtc = startedUtc,
        CompletedUtc = startedUtc.AddSeconds(4),
        ProviderId = "ap1",
        ProviderName = "Grok / xAI",
        ModelId = "grok-4-0709",
        InputTokens = 12974,
        OutputTokens = 908,
        TotalTokens = 13882,
        DurationSeconds = 4,
        Steps =
        [
            new()
            {
                Id = "appearance",
                Label = "Appearance",
                Status = "completed",
                StartedUtc = startedUtc,
                CompletedUtc = startedUtc.AddSeconds(1.1),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 1840,
                OutputTokens = 312,
                TotalTokens = 2152,
                DurationSeconds = 1.1,
                SystemPrompt = "You update character scene state. Return JSON only.",
                UserPrompt = "Characters:\nBella\nJake\nGemma\n\nTranscript:\nNarrator: Bella knocks once and lets herself into the apartment...",
                RawOutput = "{\"characters\":{\"Bella\":\"Standing just inside the apartment entrance...\"}}",
                StructuredOutputJson = "{\"characters\":{\"Bella\":\"Standing just inside the apartment entrance...\"}}"
            },
            new()
            {
                Id = "planning",
                Label = "Planning",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(1.1),
                CompletedUtc = startedUtc.AddSeconds(3.2),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 3104,
                OutputTokens = 441,
                TotalTokens = 3545,
                DurationSeconds = 1.4,
                SystemPrompt = "Produce a structured dramatic plan before prose. Return JSON only.",
                UserPrompt = "Actor: Bella...",
                RawOutput = "{\"beat\":\"Arrival and de-escalation attempt.\",\"intent\":\"Acknowledge both people without choosing sides.\"}",
                StructuredOutputJson = "{\"beat\":\"Arrival and de-escalation attempt.\",\"intent\":\"Acknowledge both people without choosing sides.\"}"
            },
            new()
            {
                Id = "prose",
                Label = "Prose",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(3.2),
                CompletedUtc = startedUtc.AddSeconds(4),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 5820,
                OutputTokens = 67,
                TotalTokens = 5887,
                DurationSeconds = 0.8,
                SystemPrompt = "Write polished contemporary roleplay prose.",
                UserPrompt = "Narrator opening beat...",
                RawOutput = "Bella knocks once and lets herself into the apartment's open living space..."
            }
        ]
    };

    static RpTurnTrace BellaTrace(DateTime startedUtc) => new()
    {
        Summary = "Completed · Bella · Appearance -> Selection -> Planning -> Prose",
        Status = "completed",
        StartedUtc = startedUtc,
        CompletedUtc = startedUtc.AddSeconds(4),
        ProviderId = "ap1",
        ProviderName = "Grok / xAI",
        ModelId = "grok-4-0709",
        InputTokens = 12874,
        OutputTokens = 829,
        TotalTokens = 13703,
        DurationSeconds = 4,
        Steps =
        [
            new()
            {
                Id = "appearance",
                Label = "Appearance",
                Status = "completed",
                StartedUtc = startedUtc,
                CompletedUtc = startedUtc.AddSeconds(0.9),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 2104,
                OutputTokens = 298,
                TotalTokens = 2402,
                DurationSeconds = 0.9,
                SystemPrompt = "You update character scene state. Return JSON only.",
                UserPrompt = "Bella wrote a greeting and touched both characters. Update appearance.",
                RawOutput = "{\"characters\":{\"Bella\":\"Standing at the dining table between Jake and Gemma.\"}}",
                StructuredOutputJson = "{\"characters\":{\"Bella\":\"Standing at the dining table between Jake and Gemma.\"}}"
            },
            new()
            {
                Id = "selection",
                Label = "Selection",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(0.9),
                CompletedUtc = startedUtc.AddSeconds(1.4),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 2380,
                OutputTokens = 74,
                TotalTokens = 2454,
                DurationSeconds = 0.5,
                SystemPrompt = "Choose the next responder. Return JSON only.",
                UserPrompt = "Scene transcript and characters...",
                RawOutput = "{\"characterName\":\"Gemma\",\"reason\":\"Bella's greeting hangs in the air and Gemma's response carries the most dramatic weight.\"}",
                StructuredOutputJson = "{\"characterName\":\"Gemma\",\"reason\":\"Bella's greeting hangs in the air and Gemma's response carries the most dramatic weight.\"}"
            },
            new()
            {
                Id = "planning",
                Label = "Planning",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(1.4),
                CompletedUtc = startedUtc.AddSeconds(2.6),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 3340,
                OutputTokens = 388,
                TotalTokens = 3728,
                DurationSeconds = 1.2,
                SystemPrompt = "Produce a structured dramatic plan before prose. Return JSON only.",
                UserPrompt = "Actor: Bella...",
                RawOutput = "{\"beat\":\"Arrival and de-escalation attempt.\",\"intent\":\"Acknowledge both people without choosing sides.\"}",
                StructuredOutputJson = "{\"beat\":\"Arrival and de-escalation attempt.\",\"intent\":\"Acknowledge both people without choosing sides.\"}"
            },
            new()
            {
                Id = "prose",
                Label = "Prose",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(2.6),
                CompletedUtc = startedUtc.AddSeconds(4),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 5050,
                OutputTokens = 69,
                TotalTokens = 5119,
                DurationSeconds = 1.4,
                SystemPrompt = "Write polished contemporary roleplay prose.",
                UserPrompt = "Character: Bella...",
                RawOutput = "Bella crosses to the table, rests a gentle hand on Jake's shoulder then Gemma's bare midriff..."
            }
        ]
    };

    static RpTurnTrace GemmaTrace(DateTime startedUtc) => new()
    {
        Summary = "Completed · Gemma · Appearance -> Selection -> Planning -> Prose",
        Status = "completed",
        StartedUtc = startedUtc,
        CompletedUtc = startedUtc.AddSeconds(3.3),
        ProviderId = "ap1",
        ProviderName = "Grok / xAI",
        ModelId = "grok-4-0709",
        InputTokens = 11930,
        OutputTokens = 774,
        TotalTokens = 12704,
        DurationSeconds = 3.3,
        Steps =
        [
            new()
            {
                Id = "appearance",
                Label = "Appearance",
                Status = "completed",
                StartedUtc = startedUtc,
                CompletedUtc = startedUtc.AddSeconds(0.9),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 2104,
                OutputTokens = 298,
                TotalTokens = 2402,
                DurationSeconds = 0.9,
                SystemPrompt = "Maintain character positions.",
                UserPrompt = "Bella wrote a greeting and touched both characters. Update appearance.",
                RawOutput = "{\"characters\":{\"Bella\":\"Standing at the dining table between Jake and Gemma.\"}}",
                StructuredOutputJson = "{\"characters\":{\"Bella\":\"Standing at the dining table between Jake and Gemma.\"}}"
            },
            new()
            {
                Id = "selection",
                Label = "Selection",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(0.9),
                CompletedUtc = startedUtc.AddSeconds(1.4),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 2380,
                OutputTokens = 74,
                TotalTokens = 2454,
                DurationSeconds = 0.5,
                SystemPrompt = "Determine responder.",
                UserPrompt = "Who responds next?",
                RawOutput = "{\"characterName\":\"Gemma\",\"reason\":\"Bella's greeting hangs in the air and Gemma's response carries the most dramatic weight.\"}",
                StructuredOutputJson = "{\"characterName\":\"Gemma\",\"reason\":\"Bella's greeting hangs in the air and Gemma's response carries the most dramatic weight.\"}"
            },
            new()
            {
                Id = "planning",
                Label = "Planning",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(1.4),
                CompletedUtc = startedUtc.AddSeconds(2.6),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 3340,
                OutputTokens = 388,
                TotalTokens = 3728,
                DurationSeconds = 1.2,
                SystemPrompt = "Plan Gemma's response.",
                UserPrompt = "Character: Gemma. Turn shape: Brief.",
                RawOutput = "{\"beat\":\"Deflection through warmth.\",\"intent\":\"Accept Bella's affection without appearing vulnerable.\"}",
                StructuredOutputJson = "{\"beat\":\"Deflection through warmth.\",\"intent\":\"Accept Bella's affection without appearing vulnerable.\"}"
            },
            new()
            {
                Id = "prose",
                Label = "Prose",
                Status = "completed",
                StartedUtc = startedUtc.AddSeconds(2.6),
                CompletedUtc = startedUtc.AddSeconds(3.3),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 4106,
                OutputTokens = 58,
                TotalTokens = 4164,
                DurationSeconds = 0.7,
                SystemPrompt = "Write Gemma's turn.",
                UserPrompt = "Return Bella's greeting warmly, land a quiet dig at Jake.",
                RawOutput = "Gemma tips her chin up toward Bella without turning..."
            }
        ]
    };

    static RpTurnTrace SnapshotTrace(DateTime startedUtc) => new()
    {
        Summary = "Completed · Snapshot",
        Status = "completed",
        StartedUtc = startedUtc,
        CompletedUtc = startedUtc.AddSeconds(0.8),
        ProviderId = "ap1",
        ProviderName = "Grok / xAI",
        ModelId = "grok-4-0709",
        InputTokens = 1420,
        OutputTokens = 94,
        TotalTokens = 1514,
        DurationSeconds = 0.8,
        Steps =
        [
            new()
            {
                Id = "snapshot",
                Label = "Snapshot",
                Status = "completed",
                StartedUtc = startedUtc,
                CompletedUtc = startedUtc.AddSeconds(0.8),
                ProviderId = "ap1",
                ProviderName = "Grok / xAI",
                ModelId = "grok-4-0709",
                InputTokens = 1420,
                OutputTokens = 94,
                TotalTokens = 1514,
                DurationSeconds = 0.8,
                SystemPrompt = "You summarize the state of an interactive roleplay scene for future continuation. Return concise JSON only.",
                UserPrompt = "Transcript and appearance state after Bella enters the room.",
                RawOutput = "{\"summary\":\"Bella has entered the apartment and physically bridged Jake and Gemma without taking sides.\"}",
                StructuredOutputJson = "{\"summary\":\"Bella has entered the apartment and physically bridged Jake and Gemma without taking sides.\"}"
            }
        ]
    };
}

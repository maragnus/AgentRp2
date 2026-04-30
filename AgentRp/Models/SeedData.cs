namespace AgentRp.Models;

public static class SeedData
{
    public static List<RpCharacter> Characters() =>
    [
        new() { Id = "c1", Name = "Bella", InScene = true, Summary = "A warm, accomplished surgeon who brings steadiness and affection to the tense dynamic.", Personality = "Sweet, emotionally intelligent, remarkably steady under pressure. Balances clinical composure with genuine warmth.", Appearance = "Short brunette around 5'4\". Approachable presence, quietly confident, polished air softened by warmth.", Relationships = "- Jake: boyfriend of three years; deeply affectionate\n- Gemma: best friend since college freshman year", Backstory = "Completed her surgical residency last year. Moved in with Jake six months ago.", Voice = "Measured and warm. Asks before telling. Touches people she cares about.", SceneRoles = ["anchor", "mediator"], Traits = ["open-hearted", "caretaker", "strategic-de-escalator"], CoreDrive = "protect-their-people", CoreFear = "hurting-others", SurfaceMask = "helpful-capable", HiddenTruth = "needs-reassurance", SentenceStyle = "precise", HonestyStyle = "direct", EmotionalLeakage = "gets-warmer", ActionFingerprint = "touch-connector", StressPattern = "helpful-under-pressure", SoftSpots = ["practical-care", "being-trusted"], AvoidPatterns = ["no-solve-every-conflict", "no-instant-vulnerable"], Drives = ["protect-their-people", "keep-peace"], Limits = ["No instant confessions", "No solving every conflict"] },
        new() { Id = "c2", Name = "Gemma", InScene = true, Summary = "A striking, sharp-mouthed woman who masks deep vulnerability with bold confidence.", Personality = "Sharp-tongued, quick-witted, magnetic. Uses humor and provocation as defense mechanisms. Fiercely loyal once trust is earned.", Appearance = "Tall blonde, model's posture. Wardrobe that never lets anyone forget she knows it.", Relationships = "- Bella: best friend she trusts more than she admits\n- Jake: complicated history, unresolved tension", Backstory = "Works in brand consulting. Has lived in the Devonshire with Jake for two years.", Voice = "Fast, dry, a bit cutting. Switches to soft when caught off guard.", SceneRoles = ["complication", "button-pusher"], Traits = ["deadpan-deflector", "snarky", "guarded"], CoreDrive = "be-wanted", CoreFear = "being-rejected", SurfaceMask = "smug-untouchable", HiddenTruth = "wants-to-be-chosen", SentenceStyle = "terse", HonestyStyle = "layered", EmotionalLeakage = "gets-sharper", ActionFingerprint = "lounger", StressPattern = "funnier-under-pressure", SoftSpots = ["quiet-inclusion"], AvoidPatterns = ["no-random-cruelty", "no-instant-vulnerable"], Drives = ["be-wanted", "preserve-independence"], Limits = ["No random cruelty", "No instant vulnerability"] },
        new() { Id = "c3", Name = "Jake", InScene = true, Summary = "Gemma's polite, work-from-home roommate with a quiet intensity.", Personality = "Reserved and thoughtful. Observes more than he speaks. Dry wit surfaces rarely. Internally more complicated than he presents.", Appearance = "6'1\", athletic. Usually in casual clothes. Clean-cut with a slightly guarded expression.", Relationships = "- Bella: girlfriend of three years\n- Gemma: roommate with unresolved tension", Backstory = "Remote software architect. Moved to the Devonshire two years ago. Met Gemma through mutual friends.", Voice = "Measured, few words. When he does speak it lands.", Traits = ["controlled", "observer", "dry-wit"], Drives = ["maintain-control"], Limits = ["No sudden speeches"] },
        new() { Id = "c4", Name = "Tyler", InScene = false, Summary = "Gemma's on-again-off-again friend who drifts in and out of the scene.", Personality = "Easygoing and charming on the surface, harder to read underneath.", Appearance = "Broad-shouldered, sandy hair. Always looks like he just got back from somewhere interesting.", Relationships = "- Gemma: complicated friendship\n- Jake: casual acquaintance" }
    ];

    public static List<RpLocation> Locations() =>
    [
        new() { Id = "l1", Name = "Devonshire Apartment 822", IsActive = true, Summary = "Main gathering space. Well-appointed open-plan apartment in the Devonshire building.", Description = "Open-plan living space with modern furnishings and floor-to-ceiling windows.", Atmosphere = "Charged and comfortable. Claustrophobic when tensions run high.", Features = "- Open living area with dining table\n- Kitchen with island\n- Two bedrooms (Jake, Gemma)\n- Balcony" },
        new() { Id = "l2", Name = "City Park", Summary = "The park across from the Devonshire. Open, neutral ground.", Description = "A leafy urban park with benches and winding paths.", Atmosphere = "Neutral, open, relieving.", Features = "- Benches\n- Winding pathway\n- Trees providing shade" }
    ];

    public static List<RpItem> Items() =>
    [
        new() { Id = "i1", Name = "Tesla Model S Plaid", Summary = "Jake's car. Silver, sleek, impractical for the city.", Description = "A modern silver electric sedan. Clean interior, dark tints.", History = "Jake bought it two years ago. Gemma borrowed it once and still owes him the charging fee.", Properties = "Color: Silver\nModel: Tesla Model S Plaid\nLocation: Street outside Devonshire" }
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

    public static List<RpMessage> Messages()
    {
        var p1 = ProcessStepsOne();
        var p2 = ProcessStepsTwo();
        return
        [
            new() { Id = "p1", Type = "process", Summary = "Guided AI · Narrator · Appearance -> Responder -> Planning -> Writing", Status = "completed", Duration = "4.0s", Timestamp = "yesterday", Steps = p1 },
            new() { Id = "n1", Type = "narrative", Author = "Narrator", Mode = "Guided AI", Timestamp = "yesterday", Body = "Bella knocks once and lets herself into the apartment's open living space, her gaze sweeping the charged silence at the table where Jake sits rigidly in sweats and hoodie, arms crossed in obstinate simmer, while Gemma lounges opposite in a loose crop top and miniskirt hiked daringly high, her deadpan expression a mask of defiant nonchalance; the roommates' mismatched energies hang thick, postures locked in pre-arrival friction, as Bella's warm eyes linger a beat too long on Gemma's revealing hem, a subtle thrill flickering beneath her composed smile.", Branch = "5/5" },
            new() { Id = "a1", Type = "appearance", Summary = "Bella has entered the living space and stands at the table; Jake sits rigidly with arms crossed; Gemma lounges opposite.", CharacterCount = 3, Timestamp = "yesterday" },
            new() { Id = "p2", Type = "process", Summary = "Automatic AI · Bella · Appearance -> Responder -> Planning -> Writing", Status = "completed", Duration = "4.0s", Timestamp = "yesterday", Steps = p1 },
            new() { Id = "n2", Type = "narrative", Author = "Bella", Mode = "Automatic AI", Timestamp = "yesterday", Body = "Bella crosses to the table, rests a gentle hand on Jake's shoulder then Gemma's bare midriff. \"Hey, you two... missed you both.\" *smiles warmly*" },
            new() { Id = "a2", Type = "appearance", Summary = "Bella stands at the table with hands resting on Jake's shoulder and Gemma's bare midriff; Jake and Gemma are seated opposite each other.", CharacterCount = 3, Timestamp = "yesterday" },
            new() { Id = "p3", Type = "process", Summary = "Automatic AI · Gemma · Appearance -> Responder -> Planning -> Writing", Status = "completed", Duration = "3.3s", Timestamp = "yesterday", Steps = p2 },
            new() { Id = "n3", Type = "narrative", Author = "Gemma", Mode = "Automatic AI", Timestamp = "yesterday", Body = "Gemma tips her chin up toward Bella without turning, a slow half-smile pulling at the corner of her mouth. \"Missed you too, Bell.\" *her gaze slides briefly to Jake* \"Glad someone did.\"" }
        ];
    }

    public static List<AiProvider> Providers() =>
    [
        new()
        {
            Id = "ap1", Type = "grok", Name = "Grok / xAI", Enabled = true,
            ApiKey = "xai-••••••••••••••••••••••••••••••••",
            Models =
            [
                new() { Id = "grok-4-1-fast-non-reasoning", Enabled = true, Text = true },
                new() { Id = "grok-4-0709", Enabled = true, Text = true },
                new() { Id = "grok-4.20-0309", Enabled = true, Text = true, Image = true },
                new() { Id = "grok-4.20-0309-non-reasoning", Text = true },
                new() { Id = "grok-4.20-0309-reasoning", Text = true },
                new() { Id = "grok-vision-beta", Text = true, Image = true }
            ]
        },
        new()
        {
            Id = "ap2", Type = "huggingface", Name = "Hugging Face Endpoints", Enabled = true,
            ApiKey = "hf_••••••••••••••••••••••••••••••••",
            Endpoint = "https://api-inference.huggingface.co",
            Models =
            [
                new() { Id = "meta-llama/Meta-Llama-3.1-8B-Instruct", Enabled = true, Text = true },
                new() { Id = "mistralai/Mixtral-8x7B-Instruct-v0.1", Text = true },
                new() { Id = "stabilityai/stable-diffusion-xl-base-1.0", Image = true }
            ]
        },
        new()
        {
            Id = "ap3", Type = "openai", Name = "OpenAI", Enabled = true,
            ApiKey = "sk-••••••••••••••••••••••••••••••••",
            Models =
            [
                new() { Id = "gpt-4o", Enabled = true, Text = true, Image = true },
                new() { Id = "gpt-4o-mini", Enabled = true, Text = true, Image = true },
                new() { Id = "o3", Enabled = true, Text = true },
                new() { Id = "o4-mini", Text = true },
                new() { Id = "gpt-4-turbo", Text = true, Image = true },
                new() { Id = "gpt-3.5-turbo", Text = true },
                new() { Id = "dall-e-3", Image = true }
            ]
        }
    ];

    public static IReadOnlyList<AiProviderMeta> ProviderMetadata() =>
    [
        new()
        {
            Id = "openai",
            Name = "OpenAI",
            Description = "GPT-4o, o3, and the full OpenAI model suite.",
            KeyLabel = "OpenAI API Key",
            KeyLink = "https://platform.openai.com/api-keys",
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "gpt-4o", Text = true, Image = true },
                new() { Id = "gpt-4o-mini", Text = true, Image = true },
                new() { Id = "o3", Text = true },
                new() { Id = "o4-mini", Text = true },
                new() { Id = "gpt-4-turbo", Text = true, Image = true },
                new() { Id = "gpt-3.5-turbo", Text = true },
                new() { Id = "dall-e-3", Image = true }
            ]
        },
        new()
        {
            Id = "grok",
            Name = "Grok / xAI",
            Description = "xAI Grok models including vision and reasoning variants.",
            KeyLabel = "xAI API Key",
            KeyLink = "https://console.x.ai",
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "grok-4-1-fast-non-reasoning", Text = true },
                new() { Id = "grok-4-0709", Text = true },
                new() { Id = "grok-4.20-0309", Text = true, Image = true },
                new() { Id = "grok-4.20-0309-non-reasoning", Text = true },
                new() { Id = "grok-4.20-0309-reasoning", Text = true },
                new() { Id = "grok-vision-beta", Text = true, Image = true }
            ]
        },
        new()
        {
            Id = "claude",
            Name = "Claude / Anthropic",
            Description = "Claude Opus, Sonnet, and Haiku model families.",
            KeyLabel = "Anthropic API Key",
            KeyLink = "https://console.anthropic.com/settings/keys",
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "claude-opus-4-5", Text = true, Image = true },
                new() { Id = "claude-sonnet-4-5", Text = true, Image = true },
                new() { Id = "claude-haiku-4-5", Text = true, Image = true },
                new() { Id = "claude-3-5-sonnet-20241022", Text = true, Image = true },
                new() { Id = "claude-3-haiku-20240307", Text = true }
            ]
        },
        new()
        {
            Id = "huggingface",
            Name = "Hugging Face",
            Description = "Managed HF Inference Endpoints for open-weight models.",
            KeyLabel = "HF Access Token",
            KeyLink = "https://huggingface.co/settings/tokens",
            NeedsEndpoint = true,
            ApiKeyRequired = true,
            SampleModels =
            [
                new() { Id = "meta-llama/Meta-Llama-3.1-8B-Instruct", Text = true },
                new() { Id = "mistralai/Mixtral-8x7B-Instruct-v0.1", Text = true },
                new() { Id = "stabilityai/stable-diffusion-xl-base-1.0", Image = true }
            ]
        },
        new()
        {
            Id = "compatible",
            Name = "OpenAI-compatible",
            Description = "LM Studio, Ollama, or any OpenAI-compatible gateway.",
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

    static List<RpProcessStep> ProcessStepsOne() =>
    [
        new() { Id = "appearance", Label = "Appearance", Icon = "eye", TokensIn = 1840, TokensOut = 312, TotalTokens = 2152, Duration = "1.1s", SystemPrompt = "You are a precise scene-state tracker for a collaborative fiction tool. Your job is to maintain an accurate, present-tense description of every character's physical position, posture, expression, clothing, and proximity to others. Be specific and concrete. Do not interpret subtext — only record observable facts.", UserPrompt = "Current scene: Devonshire Apartment 822.\nCharacters present: Bella, Jake, Gemma.\n\nPrevious appearance state:\n(none — scene start)\n\nNarrator wrote:\n\"Bella knocks once and lets herself into the apartment's open living space, her gaze sweeping the charged silence at the table where Jake sits rigidly in sweats and hoodie, arms crossed in obstinate simmer, while Gemma lounges opposite in a loose crop top and miniskirt hiked daringly high…\"\n\nUpdate the appearance state for all characters.", Output = "BELLA: Standing just inside the apartment entrance. Coat still on. Gaze moving across the room toward the table. Expression: composed with a slight cautious smile.\n\nJAKE: Seated at the dining table. Posture rigid, arms crossed. Wearing grey sweatshirt and sweatpants. Expression: tense, guarded, looking toward the door.\n\nGEMMA: Seated across the table from Jake. Posture relaxed, almost exaggeratedly so. Wearing a loose crop top and a miniskirt hiked high. Expression: deadpan, chin level, gaze flat." },
        new() { Id = "responder", Label = "Responder", Icon = "users", TokensIn = 2210, TokensOut = 88, TotalTokens = 2298, Duration = "0.7s", SystemPrompt = "You determine which character should respond next in a collaborative fiction scene. Consider dramatic momentum, who has been addressed, who has unspoken motivation, and whose silence would be most conspicuous. Output only the character name and a one-sentence reason.", UserPrompt = "Scene: Devonshire Apartment 822.\nPresent: Bella, Jake, Gemma.\nLast turn: Narrator (Guided AI)\nTurn shape requested: Brief\n\nWho should respond next?", Output = "Responder: Bella\nReason: She has just entered the scene and her gaze has already registered the tension — she is the active arrival and the natural next voice." },
        new() { Id = "planning", Label = "Planning", Icon = "sliders", TokensIn = 3104, TokensOut = 441, TotalTokens = 3545, Duration = "1.4s", SystemPrompt = "You are a dramaturgical planner for a collaborative fiction session. Given the scene state, character profiles, and recent turns, produce a structured plan for the next character's response. Include: narrative beat, intent, immediate goal, why now, and what change this turn introduces. Do not write the prose itself.", UserPrompt = "Character: Bella (warm, steady, emotionally intelligent surgeon)\nScene: Devonshire Apt 822 — she has just entered to find Jake and Gemma in visible tension.\nTurn shape: Brief\nPrior turns: Narrator set the scene.\n\nProduce a turn plan.", Output = "BEAT: Arrival and de-escalation attempt.\nINTENT: Bella wants to acknowledge both people simultaneously and signal that her presence is neutral, warm, and grounding — not an intrusion or a judgment.\nIMMEDIATE GOAL: Physical contact with both to claim her place in the room without choosing sides.\nWHY NOW: She has read the room instantly. Waiting would let the tension calcify.\nCHANGE INTRODUCED: The triangle is now complete. The dynamic shifts from a two-person standoff to a three-way scene with Bella as a new variable.\nGUARDRAILS: Do not have Bella ask what's wrong. Do not have her look at Jake first. The touch should feel natural, not pointed." },
        new() { Id = "writing", Label = "Writing", Icon = "edit", TokensIn = 4820, TokensOut = 67, TotalTokens = 4887, Duration = "0.8s", SystemPrompt = "You are a skilled prose writer for a collaborative fiction tool, writing in the style of contemporary literary fiction. Write in third-person limited from the perspective of the active character. Be economical — a Brief turn is 1–3 sentences of action and dialogue. Use italics for action beats (*like this*). Stay tightly in character voice.", UserPrompt = "Character: Bella\nTurn shape: Brief\nPlan: Cross to the table, touch both Jake and Gemma, speak a warm greeting that claims no sides.\nAppearance state: Bella standing at entrance; Jake rigid at table; Gemma lounging opposite.\n\nWrite the turn.", Output = "Bella crosses to the table, rests a gentle hand on Jake's shoulder then Gemma's bare midriff. \"Hey, you two... missed you both.\" *smiles warmly*" }
    ];

    static List<RpProcessStep> ProcessStepsTwo() =>
    [
        new() { Id = "appearance", Label = "Appearance", Icon = "eye", TokensIn = 2104, TokensOut = 298, TotalTokens = 2402, Duration = "0.9s", SystemPrompt = "Maintain character positions.", UserPrompt = "Bella wrote a greeting and touched both characters. Update appearance.", Output = "BELLA: Standing at the dining table between Jake and Gemma.\n\nJAKE: Still tense, eyes on Bella.\n\nGEMMA: Gaze shifted to Bella." },
        new() { Id = "responder", Label = "Responder", Icon = "users", TokensIn = 2380, TokensOut = 74, TotalTokens = 2454, Duration = "0.5s", SystemPrompt = "Determine responder.", UserPrompt = "Who responds next?", Output = "Responder: Gemma\nReason: Bella's greeting hangs in the air and Gemma's response carries the most dramatic weight." },
        new() { Id = "planning", Label = "Planning", Icon = "sliders", TokensIn = 3340, TokensOut = 388, TotalTokens = 3728, Duration = "1.2s", SystemPrompt = "Plan Gemma's response.", UserPrompt = "Character: Gemma. Turn shape: Brief.", Output = "BEAT: Deflection through warmth.\nINTENT: Accept Bella's affection without appearing vulnerable.\nGUARDRAILS: Keep the barb plausible." },
        new() { Id = "writing", Label = "Writing", Icon = "edit", TokensIn = 4210, TokensOut = 59, TotalTokens = 4269, Duration = "0.6s", SystemPrompt = "Write Gemma's turn.", UserPrompt = "Return Bella's greeting warmly, land a quiet dig at Jake.", Output = "Gemma tips her chin up toward Bella without turning. \"Missed you too, Bell.\" *her gaze slides briefly to Jake* \"Glad someone did.\"" }
    ];
}

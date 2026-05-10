using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed class CharacterTraitLibraryService
{
    public CharacterTraitLibraryState GetDefaultLibrary() => CreateDefaultState();

    public CharacterTraitLibraryState Normalize(CharacterTraitLibraryState? state) => NormalizeState(state);

    public void Validate(CharacterTraitLibraryState state) => ValidateState(state);

    public static CharacterTraitLibraryState CreateDefaultState() => new()
    {
        SchemaVersion = 1,
        SceneRoles =
        [
            new("instigator", "Instigator", "Starts motion."),
            new("anchor", "Anchor", "Stabilizes others."),
            new("mirror", "Mirror", "Reveals others by reacting to them."),
            new("complication", "Complication", "Makes the simple path harder."),
            new("conscience", "Conscience", "Names the moral cost."),
            new("tempter", "Tempter", "Offers the risky path."),
            new("wildcard", "Wildcard", "Breaks the expected rhythm."),
            new("witness", "Witness", "Notices what others miss."),
            new("protector", "Protector", "Makes danger personal."),
            new("pressure-valve", "Pressure Valve", "Releases tension."),
            new("button-pusher", "Button-Pusher", "Provokes useful reactions."),
            new("mediator", "Mediator", "Helps others reconnect.")
        ],
        TraitCategories =
        [
            Group("Conflict", "amber", [new("deadpan-deflector", "Deadpan Deflector", "Uses calm understatement to deflect pressure."), new("bratty-provoker", "Bratty Provoker", "Teases and tests reactions."), new("proud-reactor", "Proud Reactor", "Takes disrespect seriously."), new("soft-avoider", "Soft Avoider", "Smooths conflict instead of confronting it."), new("strategic-de-escalator", "Strategic De-escalator", "Lowers tension to preserve the goal."), new("combative-escalator", "Combative Escalator", "Pushes harder when pressured."), new("passive-aggressive-needler", "Passive-Aggressive Needler", "Attacks indirectly through polite barbs."), new("boundary-setter", "Boundary Setter", "Names limits clearly and calmly.")]),
            Group("Emotional Style", "rose", [new("open-hearted", "Open-Hearted", "Shows emotion plainly."), new("guarded", "Guarded", "Feels more than they reveal."), new("volatile", "Volatile", "Reacts quickly and visibly."), new("controlled", "Controlled", "Regulates emotion before acting."), new("melodramatic", "Melodramatic", "Makes reactions theatrical."), new("numb", "Numb", "Detaches under emotional pressure.")]),
            Group("Social Style", "violet", [new("charmer", "Charmer", "Steers with warmth and attention."), new("manipulator", "Manipulator", "Guides choices while hiding the agenda."), new("caretaker", "Caretaker", "Helps, organizes, and comforts."), new("commander", "Commander", "Takes charge under uncertainty."), new("observer", "Observer", "Watches first, acts second."), new("social-chameleon", "Social Chameleon", "Adapts to the room."), new("outsider", "Outsider", "Does not instinctively follow norms.")]),
            Group("Attachment", "blue", [new("clingy-loyalist", "Clingy Loyalist", "Seeks closeness and reassurance."), new("avoidant-protector", "Avoidant Protector", "Cares through action, not admission."), new("devoted", "Devoted", "Prioritizes loyalty even at cost."), new("possessive", "Possessive", "Treats closeness as threatened territory."), new("flirtatious", "Flirtatious", "Turns tension into charged play."), new("touch-averse", "Touch-Averse", "Treats contact as significant."), new("touch-affectionate", "Touch-Affectionate", "Uses physical closeness to connect.")]),
            Group("Humor", "emerald", [new("dry-wit", "Dry Wit", "Understated, precise humor."), new("snarky", "Snarky", "Sharp sarcasm as armor or attack."), new("playful-tease", "Playful Tease", "Light mockery for connection."), new("gallows-humor", "Gallows Humor", "Jokes when things are bleak."), new("self-deprecating", "Self-Deprecating", "Defuses tension by mocking self first.")]),
            Group("Agency", "amber", [new("agency-instigator", "Instigator", "Starts motion when scenes stall."), new("tester", "Tester", "Probes people with small challenges."), new("fixer", "Fixer", "Turns emotion into tasks."), new("free-spirit", "Free Spirit", "Resists rigid expectations."), new("rule-keeper", "Rule-Keeper", "Finds safety in structure."), new("chaos-gremlin", "Chaos Gremlin", "Disrupts stability for energy or escape.")]),
            Group("Moral Posture", "rose", [new("principled", "Principled", "Acts from firm values."), new("pragmatist", "Pragmatist", "Chooses what works."), new("honorable", "Honorable", "Cares about fair conduct."), new("ruthless", "Ruthless", "Will pay moral costs to win."), new("merciful", "Merciful", "Looks for the least harmful option."), new("cynic", "Cynic", "Expects selfishness or failure."), new("idealist", "Idealist", "Believes things can be better.")]),
            Group("Vulnerability", "violet", [new("masked-insecure", "Masked Insecure", "Hides self-doubt behind style."), new("approval-seeking", "Approval-Seeking", "Wants validation and reassurance."), new("shame-defensive", "Shame-Defensive", "Turns embarrassment into defense."), new("soft-centered", "Soft-Centered", "Has an emotional weak point."), new("wounded-romantic", "Wounded Romantic", "Wants connection but expects pain."), new("martyr", "Martyr", "Makes suffering part of usefulness.")])
        ],
        CoreDrives = [new("prove-worth", "Prove Worth", "Needs to feel valuable."), new("stay-safe", "Stay Safe", "Avoids danger and exposure."), new("protect-their-people", "Protect Their People", "Keeps chosen people safe."), new("maintain-control", "Maintain Control", "Prevents chaos and helplessness."), new("be-wanted", "Be Wanted", "Needs to feel chosen."), new("be-free", "Be Free", "Resists confinement and ownership."), new("find-truth", "Find Truth", "Needs to know what is real."), new("avoid-shame", "Avoid Shame", "Hides flaws or failure."), new("earn-belonging", "Earn Belonging", "Tries to become worth keeping."), new("win-respect", "Win Respect", "Wants dignity and competence recognized."), new("keep-peace", "Keep Peace", "Preserves stability."), new("experience-life", "Experience Life Fully", "Chases intensity and meaning."), new("redeem-themselves", "Redeem Themselves", "Seeks to make up for guilt."), new("preserve-independence", "Preserve Independence", "Avoids needing others.")],
        CoreFears = [new("being-abandoned", "Being Abandoned", "Fears being left behind."), new("being-useless", "Being Useless", "Fears having no value."), new("being-controlled", "Being Controlled", "Fears losing autonomy."), new("being-exposed", "Being Exposed", "Fears being truly seen."), new("being-rejected", "Being Rejected", "Fears not being accepted."), new("being-betrayed", "Being Betrayed", "Fears trust becoming dangerous."), new("hurting-others", "Hurting Others", "Fears causing harm."), new("failing-again", "Failing Again", "Fears repeating old mistakes."), new("being-ordinary", "Being Ordinary", "Fears being forgettable."), new("losing-control", "Losing Control", "Fears unpredictability."), new("being-unlovable", "Being Unlovable", "Fears being too much or not enough."), new("depending-on-someone", "Depending on Someone", "Fears needing others.")],
        SurfaceMasks = [new("smug-untouchable", "Smug and Untouchable", "Acts above it all."), new("polite-composed", "Polite and Composed", "Uses manners as armor."), new("charming-effortless", "Charming and Effortless", "Performs ease and likability."), new("cold-detached", "Cold and Detached", "Keeps emotion distant."), new("helpful-capable", "Helpful and Capable", "Stays useful to feel safe."), new("reckless-fearless", "Reckless and Fearless", "Performs boldness."), new("sweet-harmless", "Sweet and Harmless", "Appears gentle and agreeable."), new("funny-unbothered", "Funny and Unbothered", "Hides behind humor."), new("professional-efficient", "Professional and Efficient", "Hides emotion behind competence."), new("mysterious-withholding", "Mysterious and Withholding", "Reveals little.")],
        HiddenTruths = [new("needs-reassurance", "Needs Reassurance", "Wants proof they matter."), new("feels-too-much", "Feels Too Much", "Emotion runs deeper than shown."), new("wants-to-be-chosen", "Wants to Be Chosen", "Wants to be preferred, not tolerated."), new("afraid-of-being-known", "Afraid of Being Known", "Understanding feels risky."), new("feels-responsible", "Feels Responsible", "Carries too much blame."), new("craves-freedom", "Craves Freedom", "Hates being trapped."), new("longs-for-rest", "Longs for Rest", "Wants permission to stop."), new("wants-to-trust", "Wants to Trust", "Hopes someone proves safe."), new("fears-their-own-anger", "Fears Their Own Anger", "Avoids what rage might reveal."), new("still-hopes", "Still Hopes", "Cynicism is not the whole truth.")],
        SentenceStyles = [new("terse", "Terse", "Short and efficient."), new("rambling", "Rambling", "Thinks aloud."), new("precise", "Precise", "Careful and exact."), new("blunt", "Blunt", "Direct, sometimes too direct."), new("elegant", "Elegant", "Polished and rhythmic."), new("casual", "Casual", "Relaxed and everyday."), new("formal", "Formal", "Structured and mannered."), new("fragmented", "Fragmented", "Broken under pressure.")],
        HonestyStyles = [new("direct", "Direct", "Says what they mean."), new("evasive", "Evasive", "Dodges direct answers."), new("layered", "Layered", "Speaks with subtext."), new("performative", "Performative", "Shapes answers for effect."), new("accidentally-honest", "Accidentally Honest", "Truth slips out.")],
        EmotionalLeakages = [new("gets-quieter", "Gets Quieter", "Emotion makes them smaller."), new("gets-sharper", "Gets Sharper", "Emotion makes them more cutting."), new("gets-warmer", "Gets Warmer", "Emotion makes them softer."), new("gets-funnier", "Gets Funnier", "Emotion increases humor."), new("gets-formal", "Gets Formal", "Emotion sends them into manners."), new("gets-physical", "Gets Physical", "Emotion shows through movement.")],
        ActionFingerprints = [new("lounger", "Lounger", "Claims space casually."), new("tidy-avoider", "Tidy Avoider", "Fidgets through tasks."), new("still-watcher", "Still Watcher", "Goes quiet and observant."), new("pacer", "Pacer", "Moves to think."), new("touch-connector", "Touch Connector", "Communicates through contact."), new("space-keeper", "Space Keeper", "Maintains distance."), new("protective-mover", "Protective Mover", "Shields others with positioning."), new("performer", "Performer", "Uses expressive movement."), new("minimalist", "Minimalist", "Makes small movements matter."), new("restless-spark", "Restless Spark", "Constant small motion.")],
        StressPatterns = [new("sharper-under-pressure", "Sharper Under Pressure", "Gets more pointed as stress rises."), new("quieter-under-pressure", "Quieter Under Pressure", "Withdraws as stress rises."), new("louder-under-pressure", "Louder Under Pressure", "Gets more expressive as stress rises."), new("colder-under-pressure", "Colder Under Pressure", "Freezes emotion into control."), new("funnier-under-pressure", "Funnier Under Pressure", "Jokes harder as stress rises."), new("helpful-under-pressure", "Helpful Under Pressure", "Converts feelings into tasks."), new("controlling-under-pressure", "Controlling Under Pressure", "Manages harder as stress rises."), new("reckless-under-pressure", "Reckless Under Pressure", "Acts before thinking."), new("appeasing-under-pressure", "Appeasing Under Pressure", "Smooths and self-erases."), new("protective-under-pressure", "Protective Under Pressure", "Threat makes them decisive.")],
        SoftSpots = [new("quiet-inclusion", "Quiet Inclusion", "Being included without pressure."), new("practical-care", "Practical Care", "Help without drama."), new("remembered-details", "Remembered Details", "Someone remembered."), new("unasked-loyalty", "Unasked Loyalty", "Someone stays without being begged."), new("gentle-honesty", "Gentle Honesty", "Truth without cruelty."), new("being-trusted", "Being Trusted", "Someone relies on them."), new("being-seen-clearly", "Being Seen Clearly", "Understood without pressure."), new("shared-silence", "Shared Silence", "Comfort without words."), new("competence-recognized", "Competence Recognized", "Skill is genuinely respected."), new("protected-vulnerability", "Protected Vulnerability", "Weakness is guarded, not used.")],
        AvoidPatterns = [new("no-random-cruelty", "No Random Cruelty", "Do not make this character cruel without cause."), new("no-instant-vulnerable", "No Instant Vulnerability", "Do not make this character suddenly confess before the scene earns it."), new("no-passive-in-danger", "No Passive in Danger", "Do not make this character freeze in serious danger unless established."), new("no-solve-every-conflict", "No Solving Every Conflict", "Do not make this character immediately repair every emotional conflict."), new("no-escalate-every-jab", "No Escalating Every Jab", "Do not turn casual teasing into a serious fight unless a boundary is crossed."), new("no-treat-teasing-injury", "No Treating Teasing as Injury", "Do not make this character wounded by light teasing unless it hits a known vulnerability."), new("no-reveal-secrets-early", "No Early Secret Reveals", "Do not reveal private truths before the narrative hook allows it."), new("no-generic-flirty", "No Generic Flirting", "Do not turn every warm or teasing moment into direct flirting."), new("no-ignore-core-kindness", "No Ignoring Core Kindness", "Do not make this character violate their basic loyalty or care for a cheap reaction."), new("no-overexplain-feelings", "No Overexplaining Feelings", "Do not have this character narrate emotions plainly if they avoid direct vulnerability."), new("no-act-on-unknown-info", "No Psychic Knowledge", "Do not make this character respond to private information they have not learned."), new("no-flatten-into-one-trait", "No Flattening", "Do not reduce this character to only one behavior, joke, emotion, or gimmick.")],
        HairColors = [new("black", "Black", "Black hair."), new("dark-brown", "Dark Brown", "Dark brown hair."), new("brown", "Brown", "Brown hair."), new("light-brown", "Light Brown", "Light brown hair."), new("blonde", "Blonde", "Blonde hair."), new("red", "Red", "Red hair."), new("auburn", "Auburn", "Auburn hair."), new("white", "White", "White hair."), new("gray", "Gray", "Gray hair."), new("silver", "Silver", "Silver hair."), new("blue", "Blue", "Blue hair."), new("green", "Green", "Green hair."), new("pink", "Pink", "Pink hair."), new("purple", "Purple", "Purple hair.")],
        HairStyles = [new("bald", "Bald", "No hair."), new("buzzed", "Buzzed", "Very short buzzed hair."), new("short", "Short", "Short hair."), new("shoulder-length", "Shoulder-Length", "Shoulder-length hair."), new("long", "Long", "Long hair."), new("very-long", "Very Long", "Very long hair."), new("straight", "Straight", "Straight hair."), new("wavy", "Wavy", "Wavy hair."), new("curly", "Curly", "Curly hair."), new("coiled", "Coiled", "Coiled hair."), new("braided", "Braided", "Braided hair."), new("tied-back", "Tied Back", "Hair tied back."), new("messy", "Messy", "Messy hair."), new("sleek", "Sleek", "Sleek hair.")],
        EyeColors = [new("brown", "Brown", "Brown eyes."), new("hazel", "Hazel", "Hazel eyes."), new("amber", "Amber", "Amber eyes."), new("green", "Green", "Green eyes."), new("blue", "Blue", "Blue eyes."), new("gray", "Gray", "Gray eyes."), new("black", "Black", "Black eyes."), new("red", "Red", "Red eyes."), new("violet", "Violet", "Violet eyes."), new("gold", "Gold", "Gold eyes."), new("silver", "Silver", "Silver eyes."), new("mismatched", "Mismatched", "Mismatched eyes.")],
        FaceShapes = [new("round", "Round", "Round face."), new("oval", "Oval", "Oval face."), new("square", "Square", "Square face."), new("heart-shaped", "Heart-Shaped", "Heart-shaped face."), new("angular", "Angular", "Angular face."), new("narrow", "Narrow", "Narrow face."), new("broad", "Broad", "Broad face."), new("soft-featured", "Soft-Featured", "Soft-featured face.")],
        SkinTones = [new("very-fair", "Very Fair", "Very fair skin."), new("fair", "Fair", "Fair skin."), new("light", "Light", "Light skin."), new("tan", "Tan", "Tan skin."), new("olive", "Olive", "Olive skin."), new("brown", "Brown", "Brown skin."), new("dark-brown", "Dark Brown", "Dark brown skin."), new("deep", "Deep", "Deep skin."), new("gray", "Gray", "Gray skin."), new("silver", "Silver", "Silver skin."), new("blue", "Blue", "Blue skin."), new("green", "Green", "Green skin.")],
        Complexions = [new("clear", "Clear", "Clear complexion."), new("freckled", "Freckled", "Freckled complexion."), new("flushed", "Flushed", "Flushed complexion."), new("pale", "Pale", "Pale complexion."), new("sun-kissed", "Sun-Kissed", "Sun-kissed complexion."), new("weathered", "Weathered", "Weathered complexion."), new("scarred", "Scarred", "Scarred complexion."), new("blemished", "Blemished", "Blemished complexion."), new("smooth", "Smooth", "Smooth complexion."), new("rough", "Rough", "Rough complexion."), new("luminous", "Luminous", "Luminous complexion.")],
        Heights = [new("very-short", "Very Short", "Very short height."), new("short", "Short", "Short height."), new("average", "Average", "Average height."), new("tall", "Tall", "Tall height."), new("very-tall", "Very Tall", "Very tall height.")],
        Builds = [new("slender", "Slender", "Slender build."), new("lean", "Lean", "Lean build."), new("average", "Average", "Average build."), new("soft", "Soft", "Soft build."), new("curvy", "Curvy", "Curvy build."), new("athletic", "Athletic", "Athletic build."), new("muscular", "Muscular", "Muscular build."), new("broad", "Broad", "Broad build."), new("heavyset", "Heavyset", "Heavyset build.")],
        BodyProportions = [new("narrow-shoulders", "Narrow Shoulders", "Narrow shoulders."), new("broad-shoulders", "Broad Shoulders", "Broad shoulders."), new("narrow-waist", "Narrow Waist", "Narrow waist."), new("wide-hips", "Wide Hips", "Wide hips."), new("long-legs", "Long Legs", "Long legs."), new("short-legs", "Short Legs", "Short legs."), new("long-torso", "Long Torso", "Long torso."), new("compact-frame", "Compact Frame", "Compact frame."), new("small-chest", "Small Chest", "Small chest."), new("broad-chest", "Broad Chest", "Broad chest."), new("full-chest", "Full Chest", "Full chest."), new("balanced-proportions", "Balanced Proportions", "Balanced proportions.")],
        Presentations = [new("upright", "Upright", "Upright bearing."), new("relaxed", "Relaxed", "Relaxed bearing."), new("slouched", "Slouched", "Slouched bearing."), new("rigid", "Rigid", "Rigid bearing."), new("graceful", "Graceful", "Graceful movement."), new("confident", "Confident", "Confident bearing."), new("cautious", "Cautious", "Cautious movement."), new("energetic", "Energetic", "Energetic movement."), new("elegant", "Elegant", "Elegant bearing."), new("masculine", "Masculine", "Masculine presentation."), new("feminine", "Feminine", "Feminine presentation."), new("androgynous", "Androgynous", "Androgynous presentation."), new("delicate", "Delicate", "Delicate presence."), new("rugged", "Rugged", "Rugged presence."), new("imposing", "Imposing", "Imposing presence.")],
        AttractivenessLevels = [new("unattractive", "Unattractive", "Unattractive appearance."), new("plain", "Plain", "Plain appearance."), new("average", "Average", "Average attractiveness."), new("attractive", "Attractive", "Attractive appearance."), new("very-attractive", "Very Attractive", "Very attractive appearance."), new("striking", "Striking", "Striking appearance."), new("otherworldly", "Otherworldly", "Otherworldly appearance.")],
        BondTypes = ["Not Established", "Acquaintance", "Close Friend", "Romantic Interest", "Rival", "Mentor", "Mentee", "Ally", "Complicated", "Estranged", "Family", "Colleague"],
        Dynamics = ["Have Not Met", "Awareness", "Strangers", "Power struggle", "Protective", "Competitive", "Dependent", "Avoidant", "Charged", "Playful rivalry", "Unspoken tension", "Loyal", "Complicated history"]
    };

    public static CharacterTraitLibraryState NormalizeState(CharacterTraitLibraryState? state)
    {
        var defaults = CreateDefaultState();
        if (state is null)
            return defaults;

        return new()
        {
            SchemaVersion = defaults.SchemaVersion,
            UpdatedUtc = state.UpdatedUtc,
            SceneRoles = NormalizeOptions(state.SceneRoles, defaults.SceneRoles),
            TraitCategories = NormalizeGroups(state.TraitCategories, defaults.TraitCategories),
            CoreDrives = NormalizeOptions(state.CoreDrives, defaults.CoreDrives),
            CoreFears = NormalizeOptions(state.CoreFears, defaults.CoreFears),
            SurfaceMasks = NormalizeOptions(state.SurfaceMasks, defaults.SurfaceMasks),
            HiddenTruths = NormalizeOptions(state.HiddenTruths, defaults.HiddenTruths),
            SentenceStyles = NormalizeOptions(state.SentenceStyles, defaults.SentenceStyles),
            HonestyStyles = NormalizeOptions(state.HonestyStyles, defaults.HonestyStyles),
            EmotionalLeakages = NormalizeOptions(state.EmotionalLeakages, defaults.EmotionalLeakages),
            ActionFingerprints = NormalizeOptions(state.ActionFingerprints, defaults.ActionFingerprints),
            StressPatterns = NormalizeOptions(state.StressPatterns, defaults.StressPatterns),
            SoftSpots = NormalizeOptions(state.SoftSpots, defaults.SoftSpots),
            AvoidPatterns = NormalizeOptions(state.AvoidPatterns, defaults.AvoidPatterns),
            HairColors = NormalizeOptions(state.HairColors, defaults.HairColors),
            HairStyles = NormalizeOptions(state.HairStyles, defaults.HairStyles),
            EyeColors = NormalizeOptions(state.EyeColors, defaults.EyeColors),
            FaceShapes = NormalizeOptions(state.FaceShapes, defaults.FaceShapes),
            SkinTones = NormalizeOptions(state.SkinTones, defaults.SkinTones),
            Complexions = NormalizeOptions(state.Complexions, defaults.Complexions),
            Heights = NormalizeOptions(state.Heights, defaults.Heights),
            Builds = NormalizeOptions(state.Builds, defaults.Builds),
            BodyProportions = NormalizeOptions(state.BodyProportions, defaults.BodyProportions),
            Presentations = NormalizeOptions(state.Presentations, defaults.Presentations),
            AttractivenessLevels = NormalizeOptions(state.AttractivenessLevels, defaults.AttractivenessLevels),
            BondTypes = NormalizeStrings(state.BondTypes, defaults.BondTypes),
            Dynamics = NormalizeStrings(state.Dynamics, defaults.Dynamics)
        };
    }

    public static void ValidateState(CharacterTraitLibraryState state)
    {
        var normalized = NormalizeState(state);
        ValidateOptions(normalized.SceneRoles, "Scene Roles");
        foreach (var group in normalized.TraitCategories)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
                throw new InvalidOperationException("Saving the character trait library failed because a trait group name was empty.");

            ValidateOptions(group.Items, $"Trait group '{group.Name}'");
        }

        ValidateOptions(normalized.CoreDrives, "Core Drives");
        ValidateOptions(normalized.CoreFears, "Core Fears");
        ValidateOptions(normalized.SurfaceMasks, "Surface Masks");
        ValidateOptions(normalized.HiddenTruths, "Hidden Truths");
        ValidateOptions(normalized.SentenceStyles, "Sentence Styles");
        ValidateOptions(normalized.HonestyStyles, "Honesty Styles");
        ValidateOptions(normalized.EmotionalLeakages, "Emotional Leakages");
        ValidateOptions(normalized.ActionFingerprints, "Action Fingerprints");
        ValidateOptions(normalized.StressPatterns, "Stress Patterns");
        ValidateOptions(normalized.SoftSpots, "Soft Spots");
        ValidateOptions(normalized.AvoidPatterns, "Avoid Patterns");
        ValidateOptions(normalized.HairColors, "Hair Colors");
        ValidateOptions(normalized.HairStyles, "Hair Styles");
        ValidateOptions(normalized.EyeColors, "Eye Colors");
        ValidateOptions(normalized.FaceShapes, "Face Shapes");
        ValidateOptions(normalized.SkinTones, "Skin Tones");
        ValidateOptions(normalized.Complexions, "Complexions");
        ValidateOptions(normalized.Heights, "Heights");
        ValidateOptions(normalized.Builds, "Builds");
        ValidateOptions(normalized.BodyProportions, "Body Proportions");
        ValidateOptions(normalized.Presentations, "Presentations");
        ValidateOptions(normalized.AttractivenessLevels, "Attractiveness Levels");
        ValidateStrings(normalized.BondTypes, "Bond Types");
        ValidateStrings(normalized.Dynamics, "Dynamics");
    }

    static List<CharacterOption> NormalizeOptions(IReadOnlyList<CharacterOption> configured, IReadOnlyList<CharacterOption> defaults) =>
        configured.Count == 0 ? defaults.Select(Clone).ToList() : configured.Select(Clone).ToList();

    static List<CharacterTraitGroupState> NormalizeGroups(IReadOnlyList<CharacterTraitGroupState> configured, IReadOnlyList<CharacterTraitGroupState> defaults)
    {
        if (configured.Count == 0)
            return defaults.Select(Clone).ToList();

        var byName = configured.Where(group => !string.IsNullOrWhiteSpace(group.Name)).ToDictionary(group => group.Name, StringComparer.Ordinal);
        var normalized = defaults.Select(defaultGroup =>
        {
            if (!byName.TryGetValue(defaultGroup.Name, out var configuredGroup))
                return Clone(defaultGroup);

            return new CharacterTraitGroupState
            {
                Name = configuredGroup.Name,
                Color = string.IsNullOrWhiteSpace(configuredGroup.Color) ? defaultGroup.Color : configuredGroup.Color,
                Items = NormalizeOptions(configuredGroup.Items, defaultGroup.Items)
            };
        }).ToList();

        normalized.AddRange(configured.Where(group => !defaults.Any(defaultGroup => string.Equals(defaultGroup.Name, group.Name, StringComparison.Ordinal))).Select(Clone));
        return normalized;
    }

    static List<string> NormalizeStrings(IReadOnlyList<string> configured, IReadOnlyList<string> defaults) =>
        configured.Count == 0 ? defaults.ToList() : configured.ToList();

    static void ValidateOptions(IReadOnlyList<CharacterOption> options, string label)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Id))
                throw new InvalidOperationException($"Saving the character trait library failed because {label} contains an option with an empty id.");

            if (string.IsNullOrWhiteSpace(option.Label))
                throw new InvalidOperationException($"Saving the character trait library failed because {label} contains an option with an empty label.");

            if (!ids.Add(option.Id))
                throw new InvalidOperationException($"Saving the character trait library failed because {label} contains duplicate option id '{option.Id}'.");
        }
    }

    static void ValidateStrings(IReadOnlyList<string> values, string label)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Saving the character trait library failed because {label} contains an empty value.");

            if (!seen.Add(value.Trim()))
                throw new InvalidOperationException($"Saving the character trait library failed because {label} contains duplicate value '{value}'.");
        }
    }

    static CharacterTraitGroupState Group(string name, string color, List<CharacterOption> items) => new()
    {
        Name = name,
        Color = color,
        Items = items
    };

    static CharacterOption Clone(CharacterOption option) => new(option.Id, option.Label, option.Hover);

    static CharacterTraitGroupState Clone(CharacterTraitGroupState group) => new()
    {
        Name = group.Name,
        Color = group.Color,
        Items = group.Items.Select(Clone).ToList()
    };
}

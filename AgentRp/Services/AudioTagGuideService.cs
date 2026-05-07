using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public interface IAudioTagGuideService
{
    AudioTagPromptGuide BuildGuide(RpChatDocument document, IReadOnlyList<AiProvider> providers);
}

public sealed record AudioTagPromptGuide(string SystemGuide, string UserReminder)
{
    public static AudioTagPromptGuide Empty { get; } = new("", "");
    public bool HasContent => !string.IsNullOrWhiteSpace(SystemGuide) || !string.IsNullOrWhiteSpace(UserReminder);
}

public sealed class AudioTagGuideService : IAudioTagGuideService
{
    public AudioTagPromptGuide BuildGuide(RpChatDocument document, IReadOnlyList<AiProvider> providers)
    {
        if (!document.Transcript.Options.InjectAudioTags)
            return AudioTagPromptGuide.Empty;

        var selection = TextModelTuningCatalog.TryResolveActiveModel(providers, AiModelRole.Voice, document.ActiveModelSelections);
        return selection?.Provider.Type.Trim().ToLowerInvariant() switch
        {
            "elevenlabs" => ElevenLabsGuide,
            "grok" => XAiGuide,
            "xai" => XAiGuide,
            _ => AudioTagPromptGuide.Empty
        };
    }

    static readonly AudioTagPromptGuide ElevenLabsGuide = new(
        """
        Audio tag guidance for ElevenLabs v3:
        - Audio tags are enabled for this chat. Inject ElevenLabs-compatible square-bracket tags directly into the prose when they make the line more performable.
        - Use tags as actor directions, not commentary. Keep them inline with dialogue or narration, and do not explain, summarize, or list them outside the prose.
        - Most spoken turns should include one to four purposeful tags when emotion, delivery, pacing, reaction, accent, interruption, or environmental sound would improve the read. Do not tag every sentence.
        - Delivery and volume examples: [whispers], [murmurs], [softly], [quietly], [firmly], [flatly], [deadpan], [teasing], [sarcastic], [breathless], [rushed], [slowly], [shouts], [yells].
        - Emotion examples: [sad], [angry], [happily], [curious], [nervous], [relieved], [amused], [fearful], [sorrowful], [excited], [annoyed], [fondly], [bitterly], [playful].
        - Human reaction examples: [laughs], [chuckles], [scoffs], [sighs], [gasps], [clears throat], [swallows], [sniffs], [inhales], [exhales], [voice cracks].
        - Pacing and hesitation examples: [pause], [short pause], [long pause], [hesitates], [stammers], [drawn out], [trails off], [interrupts], [overlapping], [beat].
        - Accent and voice color examples: [British accent], [Southern accent], [French accent], [Irish accent], [New York accent], [tired], [hoarse], [warmly], [coldly].
        - Sound effect examples: [door creaks], [phone buzzes], [footsteps approach], [rain patters], [glass clinks], [distant thunder], [chair scrapes], [keys jingle].
        - Strong examples:
          [whispers] "Don't move."
          [sighs] "I thought we were past this."
          [angry] "You had no right to hide that from me."
          [nervous laugh] "That's... probably not the best sign."
          [long pause] "Fine. Tell me the truth."
          [French accent] "You make this sound so simple."
          [door creaks] She turns toward the hall. [softly] "Someone's here."
        """,
        """
        Audio tag reminder:
        Audio tags are enabled. Inject ElevenLabs-style square-bracket tags directly into the prose where they improve emotion, delivery, pacing, reactions, accents, or sound. Keep them inline and do not explain them.
        """);

    static readonly AudioTagPromptGuide XAiGuide = new(
        """
        Audio tag guidance for xAI text-to-speech:
        - Audio tags are enabled for this chat. Inject xAI-compatible speech tags directly into the prose when they make the line more performable.
        - Use bracketed inline tags for specific vocal expressions at a point in the line. Use XML-like wrapping tags only when a phrase or sentence needs sustained delivery. Do not invent tags beyond the supported forms below.
        - Keep tags as part of the transcript text. Do not explain the tags, put them in a separate note, or strip them from the line.
        - Most spoken turns should include one to four purposeful tags when timing, breath, laughter, crying, volume, pitch, speed, vocal style, or emphasis would improve the read. Do not tag every sentence.
        - Inline pause examples: [pause], [long-pause], [hum-tune].
        - Inline laughter and crying examples: [laugh], [chuckle], [giggle], [cry].
        - Inline mouth sound examples: [tsk], [tongue-click], [lip-smack].
        - Inline breathing examples: [breath], [inhale], [exhale], [sigh].
        - Wrapping tags for volume and intensity: <soft>soft text</soft>, <whisper>quiet text</whisper>, <loud>loud text</loud>, <build-intensity>rising text</build-intensity>, <decrease-intensity>settling text</decrease-intensity>.
        - Wrapping tags for pitch and speed: <higher-pitch>brighter text</higher-pitch>, <lower-pitch>lower text</lower-pitch>, <slow>slow text</slow>, <fast>fast text</fast>.
        - Wrapping tags for vocal style: <sing-song>playful text</sing-song>, <singing>sung text</singing>, <laugh-speak>laughing speech</laugh-speak>, <emphasis>important text</emphasis>.
        - Wrapping tags can be combined around complete phrases, such as <slow><soft>Goodnight, sleep well.</soft></slow>.
        - Strong examples:
          <whisper>Don't move.</whisper>
          [sigh] "I thought we were past this."
          <loud>Get away from the door!</loud>
          <slow>"Say that again."</slow>
          [laugh] "That's... probably not the best sign."
          [long-pause] "Fine. Tell me the truth."
          <build-intensity>"No. No, you do not get to walk away from this."</build-intensity>
          [inhale] <soft>"Someone's here."</soft>
        """,
        """
        Audio tag reminder:
        Audio tags are enabled. Inject xAI-compatible speech tags directly into the prose: supported bracket tags like [pause], [laugh], [sigh], [inhale], or [long-pause] for point cues, and supported wrapping tags like <whisper>...</whisper>, <soft>...</soft>, <loud>...</loud>, <slow>...</slow>, <build-intensity>...</build-intensity>, or <emphasis>...</emphasis> for sustained delivery. Do not explain them.
        """);
}

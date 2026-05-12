using AgentRp.Components.Chat;
using AgentRp.Components.Common;
using AgentRp.Models;

namespace AgentRp.Tests;

public sealed class SnapshotDraftEditorStateTests
{
    [Fact]
    public void FromUsesStableTimelineEntryIds()
    {
        var draft = new RpTranscriptSnapshotDraft
        {
            TimelineEntries =
            [
                new() { TurnNumber = 1, Title = "First", Description = "One" },
                new() { TurnNumber = 2, Title = "Second", Description = "Two" }
            ]
        };

        var editor = SnapshotDraftEditorState.From(draft);
        var baseline = SnapshotDraftEditorState.From(draft);

        Assert.True(StatefulFormSnapshot.Equivalent(editor, baseline));
    }
}

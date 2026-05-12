using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Components.Chat;

public sealed class ChatDirectionEditorState
{
    public string Title { get; set; } = "";
    public ChatDirectionState Direction { get; set; } = ChatDirectionState.CreateDefault();

    public static ChatDirectionEditorState From(string title, ChatDirectionState direction) => new()
    {
        Title = title,
        Direction = SessionCloner.Clone(direction)
    };
}

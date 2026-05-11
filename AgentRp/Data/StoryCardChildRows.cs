namespace AgentRp.Data;

public interface IStoryCardChildRow
{
    string Id { get; set; }
}

public interface IStoryCardTemplateChildRow : IStoryCardChildRow
{
    string StoryCardTemplateId { get; set; }
}

public interface IStoryCardInstanceChildRow : IStoryCardChildRow
{
    string ChatId { get; set; }
    string StoryCardInstanceId { get; set; }
}

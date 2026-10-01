namespace BestStories.Api;

public sealed record StoryDto(
    string Title,
    string? Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount);


public sealed record HnItem(
    int Id,
    string? Type,
    string? Title,
    string? Url,
    string? By,
    long Time,
    int Score,
    int Descendants,
    bool Deleted,
    bool Dead);

public sealed class HackerNewsOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan RefreshInterval { get; set; } 
    public TimeSpan InitialLoadTimeout { get; set; } 
    public int MaxConcurrency { get; set; } = 16;
    public int BestStoryLimit { get; set; } = 500;
}

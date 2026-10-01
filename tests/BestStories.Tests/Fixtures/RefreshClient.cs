using BestStories.Api;

namespace BestStories.Tests.Fixtures;

public sealed class RefreshClient : IHackerNewsClient
{
    private readonly Dictionary<int, HnItem> _stories;
    private readonly HashSet<int> _failedIds = [];

    public RefreshClient(Dictionary<int, HnItem> stories)
    {
        // Defensive copy prevents external mutation from leaking
        // into this test client.
        _stories = new Dictionary<int, HnItem>(stories);
    }

    public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<int>>(
            _stories.Keys.ToList());
    }

    public Task<HnItem?> GetItemAsync(
        int id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_failedIds.Contains(id))
        {
            throw new HttpRequestException(
                $"Simulated failure for {id}.");
        }

        return Task.FromResult<HnItem?>(
            _stories.TryGetValue(id, out var story)
                ? story
                : null);
    }

    public void FailItem(int id)
    {
        _failedIds.Add(id);
    }

    public void SetStory(int id, HnItem story)
    {
        _stories[id] = story;
    }
}
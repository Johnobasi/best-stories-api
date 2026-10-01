using BestStories.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BestStories.Tests;

public sealed class BestStoriesRefresherTests
{
    [Fact]
    public async Task Refresh_publishes_stories_sorted_by_score()
    {
        var client = new RefreshClient(new Dictionary<int, HnItem>
        {
            [1] = Story(1, "one", 10),
            [2] = Story(2, "two", 50),
            [3] = Story(3, "three", 20)
        });
        var cache = new BestStoriesCache();
        var refresher = CreateRefresher(client, cache);

        await refresher.RefreshAsync(CancellationToken.None);

        var stories = await cache.GetAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.NotNull(stories);
        Assert.Equal(["two", "three", "one"], stories!.Select(story => story.Title));
    }

    [Fact]
    public async Task Failed_item_uses_previous_value_when_available()
    {
        var client = new RefreshClient(new Dictionary<int, HnItem>
        {
            [1] = Story(1, "original", 10),
            [2] = Story(2, "second", 20)
        });
        var cache = new BestStoriesCache();
        var refresher = CreateRefresher(client, cache);

        await refresher.RefreshAsync(CancellationToken.None);

        client.FailItem(1);
        client.SetStory(2, Story(2, "updated", 30));

        await refresher.RefreshAsync(CancellationToken.None);

        var stories = await cache.GetAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.NotNull(stories);
        Assert.Equal("updated", stories!.Single(story => story.Title == "updated").Title);
        Assert.Contains(stories, story => story.Title == "original");
    }

    [Fact]
    public async Task Failed_item_without_previous_value_is_excluded()
    {
        var client = new RefreshClient(new Dictionary<int, HnItem>
        {
            [1] = Story(1, "one", 10),
            [2] = Story(2, "two", 20)
        });
        client.FailItem(2);

        var cache = new BestStoriesCache();
        var refresher = CreateRefresher(client, cache);

        await refresher.RefreshAsync(CancellationToken.None);

        var stories = await cache.GetAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.NotNull(stories);
        Assert.Single(stories!);
        Assert.Equal("one", stories[0].Title);
    }

    [Fact]
    public void Map_normalises_empty_url_to_null()
    {
        var dto = BestStoriesRefresher.Map(
            Story(1, "story", 10, url: ""));

        Assert.NotNull(dto);
        Assert.Null(dto!.Uri);
    }
    #region RefreshClient Helpers
    private static BestStoriesRefresher CreateRefresher(
        IHackerNewsClient client,
        BestStoriesCache cache) =>
        new(
            client,
            cache,
            Options.Create(new HackerNewsOptions
            {
                BestStoryLimit = 500,
                MaxConcurrency = 4,
                RefreshInterval = TimeSpan.FromMinutes(1)
            }),
            NullLogger<BestStoriesRefresher>.Instance);


    private static HnItem Story(
        int id,
        string title,
        int score,
        string? url = "https://example.com") =>
        new(id, "story", title, url, "author", 1570887781, score, 5, false, false);



    private sealed class RefreshClient(Dictionary<int, HnItem> stories) : IHackerNewsClient
    {
        private readonly HashSet<int> _failedIds = [];

        public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<int>>(stories.Keys.ToList());

        public Task<HnItem?> GetItemAsync(int id, CancellationToken cancellationToken)
        {
            if (_failedIds.Contains(id))
            {
                throw new HttpRequestException($"Simulated failure for {id}.");
            }

            return Task.FromResult<HnItem?>(stories[id]);
        }

        public void FailItem(int id) => _failedIds.Add(id);

        public void SetStory(int id, HnItem story) => stories[id] = story;
    }
    #endregion
}

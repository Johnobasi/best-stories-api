using BestStories.Api;
using BestStories.Tests.Fixtures;
using BestStories.Tests.TestData;
using Xunit;

namespace BestStories.Tests;

public sealed class BestStoriesRefresherTests
{
    [Fact]
    public async Task Refresh_publishes_stories_sorted_by_score()
    {
        var client = new RefreshClient(
            HackerNewsTestData.Stories());

        var cache = new BestStoriesCache();

        var refresher = BestStoriesRefresherFactory.Create(
            client,
            cache);

        await refresher.RefreshAsync(
            CancellationToken.None);

        var stories = await cache.GetAsync(
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.NotNull(stories);

        Assert.Equal(
            ["two", "three", "one"],
            stories!.Select(story => story.Title));
    }

    [Fact]
    public async Task Failed_item_uses_previous_value_when_available()
    {
        var client = new RefreshClient(
            new Dictionary<int, HnItem>
            {
                [1] = HackerNewsTestData.Story(
                    1,
                    "original",
                    10),

                [2] = HackerNewsTestData.Story(
                    2,
                    "second",
                    20)
            });

        var cache = new BestStoriesCache();

        var refresher = BestStoriesRefresherFactory.Create(
            client,
            cache);

        await refresher.RefreshAsync(
            CancellationToken.None);

        client.FailItem(1);

        client.SetStory(
            2,
            HackerNewsTestData.Story(
                2,
                "updated",
                30));

        await refresher.RefreshAsync(
            CancellationToken.None);

        var stories = await cache.GetAsync(
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.NotNull(stories);

        Assert.Contains(
            stories!,
            story => story.Title == "original");

        Assert.Contains(
            stories,
            story => story.Title == "updated");
    }

    [Fact]
    public async Task Failed_item_without_previous_value_is_excluded()
    {
        var client = new RefreshClient(
            new Dictionary<int, HnItem>
            {
                [1] = HackerNewsTestData.Story(
                    1,
                    "one",
                    10),

                [2] = HackerNewsTestData.Story(
                    2,
                    "two",
                    20)
            });

        client.FailItem(2);

        var cache = new BestStoriesCache();

        var refresher = BestStoriesRefresherFactory.Create(
            client,
            cache);

        await refresher.RefreshAsync(
            CancellationToken.None);

        var stories = await cache.GetAsync(
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.NotNull(stories);
        Assert.Single(stories!);
        Assert.Equal("one", stories[0].Title);
    }

    [Fact]
    public void Map_normalises_empty_url_to_null()
    {
        var dto = BestStoriesRefresher.Map(
            HackerNewsTestData.Story(
                1,
                "story",
                10,
                ""));

        Assert.NotNull(dto);
        Assert.Null(dto!.Uri);
    }
}
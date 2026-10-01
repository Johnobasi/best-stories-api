using System.Net;
using System.Net.Http.Json;
using BestStories.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BestStories.Tests;

public sealed class FakeClient : IHackerNewsClient
{
    public int ItemCalls;
    public bool FailBestStories { get; set; }
    public bool BlockBestStories { get; set; }

    private readonly Dictionary<int, HnItem?> _items = new()
    {
        [1] = new(1, "story", "low", "http://a", "alice", 1570887781, 10, 1, false, false),
        [2] = new(2, "story", "high", "http://b", "bob", 1570887781, 500, 20, false, false),
        [3] = new(3, "story", "mid", "", "carol", 1570887781, 100, 5, false, false),
        [4] = new(4, "comment", "nope", null, "dave", 1570887781, 999, 0, false, false),
        [5] = null,
        [6] = new(6, "story", "dead", null, "eve", 1570887781, 999, 0, false, true),
    };

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        if (BlockBestStories)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (FailBestStories)
        {
            throw new HttpRequestException("Simulated Hacker News failure.");
        }

        return _items.Keys.ToList();
    }

    public Task<HnItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ItemCalls);
        return Task.FromResult(_items[id]);
    }
}

public sealed class ApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly FakeClient _fake = new();

    private HttpClient CreateClient(Action<HackerNewsOptions>? configure = null)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHackerNewsClient>();
            services.AddSingleton<IHackerNewsClient>(_fake);

            if (configure is not null)
            {
                services.PostConfigure(configure);
            }
        })).CreateClient();
    }

    [Fact]
    public async Task Returns_top_n_sorted_by_score_desc_and_filters_non_stories()
    {
        var response = await CreateClient()
            .GetFromJsonAsync<List<StoryDto>>("/api/stories/best?n=2");

        Assert.NotNull(response);
        Assert.Equal(["high", "mid"], response!.Select(story => story.Title));
        Assert.Equal(500, response[0].Score);
        Assert.Equal(20, response[0].CommentCount);
        Assert.Equal("bob", response[0].PostedBy);
    }

    [Fact]
    public async Task N_larger_than_available_returns_all_valid_stories()
    {
        var response = await CreateClient()
            .GetFromJsonAsync<List<StoryDto>>("/api/stories/best?n=100");

        Assert.NotNull(response);
        Assert.Equal(3, response!.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?n=0")]
    [InlineData("?n=-1")]
    [InlineData("?n=abc")]
    public async Task Invalid_n_returns_400(string queryString)
    {
        var response = await CreateClient()
            .GetAsync("/api/stories/best" + queryString);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Requests_do_not_hit_upstream_after_snapshot_is_ready()
    {
        var client = CreateClient();
        await client.GetAsync("/api/stories/best?n=1");

        var before = _fake.ItemCalls;

        await Task.WhenAll(
            Enumerable.Range(0, 50)
                .Select(_ => client.GetAsync("/api/stories/best?n=3")));

        Assert.Equal(before, _fake.ItemCalls);
    }

    [Fact]
    public async Task Empty_url_is_returned_as_null()
    {
        var response = await CreateClient()
            .GetFromJsonAsync<List<StoryDto>>("/api/stories/best?n=3");

        var story = response!.Single(item => item.Title == "mid");

        Assert.Null(story.Uri);
    }

    [Fact]
    public async Task Initial_load_timeout_returns_503()
    {
        _fake.BlockBestStories = true;

        var client = CreateClient(options =>
        {
            options.InitialLoadTimeout = TimeSpan.FromMilliseconds(50);
        });

        var response = await client.GetAsync("/api/stories/best?n=1");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public void Time_is_serialised_as_iso8601_utc()
    {
        var dto = new StoryDto(
            "t",
            null,
            "u",
            DateTimeOffset.FromUnixTimeSeconds(1570887781),
            1,
            0);

        var json = System.Text.Json.JsonSerializer.Serialize(
            dto,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"time\":\"2019-10-12T13:43:01+00:00\"", json);
    }
}

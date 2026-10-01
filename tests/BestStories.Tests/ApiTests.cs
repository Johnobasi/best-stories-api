using System.Net;
using System.Net.Http.Json;
using BestStories.Api;
using BestStories.Tests.TestFixtures;
using Xunit;

namespace BestStories.Tests;

public sealed class ApiTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    private readonly FakeHackerNewsClient _fake;

    public ApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _fake = fixture.Fake;

        // Ensure each test starts from a clean fake state.
        _fake.Reset();
    }

    [Fact]
    public async Task Returns_top_n_sorted_by_score_desc_and_filters_non_stories()
    {
        var response = await _fixture
            .CreateClient()
            .GetFromJsonAsync<List<StoryDto>>(
                "/api/stories/best?n=2");

        Assert.NotNull(response);

        Assert.Equal(
            ["high", "mid"],
            response!.Select(story => story.Title));

        Assert.Equal(500, response[0].Score);
        Assert.Equal(20, response[0].CommentCount);
        Assert.Equal("bob", response[0].PostedBy);
    }

    [Fact]
    public async Task N_larger_than_available_returns_all_valid_stories()
    {
        var response = await _fixture
            .CreateClient()
            .GetFromJsonAsync<List<StoryDto>>(
                "/api/stories/best?n=100");

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
        var response = await _fixture
            .CreateClient()
            .GetAsync("/api/stories/best" + queryString);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Requests_do_not_hit_upstream_after_snapshot_is_ready()
    {
        var client = _fixture.CreateClient();

        // Ensure the snapshot has been populated.
        await client.GetAsync("/api/stories/best?n=1");

        var before = _fake.ItemCalls;

        await Task.WhenAll(
            Enumerable.Range(0, 50)
                .Select(_ =>
                    client.GetAsync("/api/stories/best?n=3")));

        // API requests should read from the existing snapshot.
        Assert.Equal(before, _fake.ItemCalls);
    }

    [Fact]
    public async Task Empty_url_is_returned_as_null()
    {
        var response = await _fixture
            .CreateClient()
            .GetFromJsonAsync<List<StoryDto>>(
                "/api/stories/best?n=3");

        Assert.NotNull(response);

        var story = response!
            .Single(item => item.Title == "mid");

        Assert.Null(story.Uri);
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
            new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains(
            "\"time\":\"2019-10-12T13:43:01+00:00\"",
            json);
    }
}
using Microsoft.Extensions.Options;

namespace BestStories.Api;

/// <summary>
/// Periodically builds the complete best-stories snapshot.
/// This is the only component that calls Hacker News, so upstream traffic is
/// independent of the number of API clients.
/// </summary>
public sealed class BestStoriesRefresher(
    IHackerNewsClient client,
    BestStoriesCache cache,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesRefresher> log) : BackgroundService
{
    private readonly HackerNewsOptions _options = options.Value;
    private Dictionary<int, StoryDto> _previous = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Hacker News refresh failed; serving the previous snapshot.");
            }

            var delay = cache.IsReady
                ? _options.RefreshInterval
                : TimeSpan.FromSeconds(2);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var ids = (await client.GetBestStoryIdsAsync(cancellationToken))
            .Take(_options.BestStoryLimit)
            .ToArray();

        var stories = new StoryDto?[ids.Length];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ids.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                var id = ids[index];

                try
                {
                    stories[index] = Map(await client.GetItemAsync(id, token));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Failed to refresh Hacker News item {Id}.", id);
                    _previous.TryGetValue(id, out var previous);
                    stories[index] = previous;
                }
            });

        var next = new Dictionary<int, StoryDto>(ids.Length);

        for (var i = 0; i < ids.Length; i++)
        {
            if (stories[i] is { } story)
            {
                next[ids[i]] = story;
            }
        }

        var snapshot = next.Values
            .OrderByDescending(story => story.Score)
            .ToArray();

        _previous = next;
        cache.Publish(snapshot);

        log.LogInformation("Hacker News snapshot refreshed with {Count} stories.", snapshot.Length);
    }

    public static StoryDto? Map(HnItem? item)
    {
        if (item is null ||
            item.Deleted ||
            item.Dead ||
            !string.Equals(item.Type, "story", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(item.Title))
        {
            return null;
        }

        return new StoryDto(
            item.Title,
            string.IsNullOrWhiteSpace(item.Url) ? null : item.Url,
            item.By ?? string.Empty,
            DateTimeOffset.FromUnixTimeSeconds(item.Time),
            item.Score,
            item.Descendants);
    }
}

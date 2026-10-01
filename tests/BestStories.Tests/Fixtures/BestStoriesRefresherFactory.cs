using BestStories.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BestStories.Tests.Fixtures;

public static class BestStoriesRefresherFactory
{
    public static BestStoriesRefresher Create(
        IHackerNewsClient client,
        BestStoriesCache cache)
    {
        return new BestStoriesRefresher(
            client,
            cache,
            Options.Create(new HackerNewsOptions
            {
                BestStoryLimit = 500,
                MaxConcurrency = 4,
                RefreshInterval = TimeSpan.FromMinutes(1)
            }),
            NullLogger<BestStoriesRefresher>.Instance);
    }
}
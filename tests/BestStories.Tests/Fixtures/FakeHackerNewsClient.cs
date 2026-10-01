using BestStories.Api;

namespace BestStories.Tests.TestFixtures
{
    public sealed class FakeHackerNewsClient : IHackerNewsClient
    {
        public void Reset()
        {
            BlockBestStories = false;
            ItemCalls = 0;
        }
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
}

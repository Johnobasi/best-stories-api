using BestStories.Api;

namespace BestStories.Tests.TestData;

public static class HackerNewsTestData
{
    public static Dictionary<int, HnItem> Stories()
    {
        return new Dictionary<int, HnItem>
        {
            [1] = Story(1, "one", 10),
            [2] = Story(2, "two", 50),
            [3] = Story(3, "three", 20)
        };
    }

    public static HnItem Story(
        int id,
        string title,
        int score,
        string? url = "https://example.com")
    {
        return new HnItem(
            id,
            "story",
            title,
            url,
            "author",
            1570887781,
            score,
            5,
            false,
            false);
    }
}
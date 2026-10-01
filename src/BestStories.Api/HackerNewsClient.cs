using System.Net.Http.Json;

namespace BestStories.Api;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct);
    Task<HnItem?> GetItemAsync(int id, CancellationToken ct);
}

public sealed class HackerNewsClient(HttpClient http) : IHackerNewsClient
{
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<int>>("beststories.json", ct) ?? [];

    public Task<HnItem?> GetItemAsync(int id, CancellationToken ct) =>
        http.GetFromJsonAsync<HnItem>($"item/{id}.json", ct);
}

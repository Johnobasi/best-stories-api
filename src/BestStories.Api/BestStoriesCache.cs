namespace BestStories.Api;

/// <summary>
/// Holds the latest immutable snapshot, sorted by score descending.
/// Reads are lock-free; the refresher swaps the reference atomically.
/// </summary>
public sealed class BestStoriesCache
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile IReadOnlyList<StoryDto>? _stories;

    public bool IsReady => _stories is not null;

    public void Publish(IReadOnlyList<StoryDto> stories)
    {
        _stories = stories;
        _ready.TrySetResult();
    }

    /// <summary>Returns the snapshot, or null if the first load hasn't completed within <paramref name="timeout"/>.</summary>
    public async ValueTask<IReadOnlyList<StoryDto>?> GetAsync(TimeSpan timeout, CancellationToken ct)
    {
        var s = _stories;
        if (s is not null) return s;
        try
        {
            await _ready.Task.WaitAsync(timeout, ct);
            return _stories;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}

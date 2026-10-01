using Microsoft.Extensions.Options;

namespace BestStories.Api.Endpoints;

public static class StoryEndpoints
{
    public static IEndpointRouteBuilder MapStoryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/stories/best",
            async (
                int? n,
                BestStoriesCache cache,
                IOptions<HackerNewsOptions> options,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (n is null || n < 1)
                {
                    return Results.Problem(
                        "Query parameter 'n' is required and must be a positive integer.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var stories = await cache.GetAsync(
                    options.Value.InitialLoadTimeout,
                    cancellationToken);

                if (stories is null)
                {
                    return Results.Problem(
                        "Story data is not available yet. Retry shortly.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                httpContext.Response.Headers.CacheControl =
                    "public, max-age=15";

                return Results.Ok(stories.Take(n.Value));
            })
            .WithName("GetBestStories")
            .WithTags("Stories");

        return endpoints;
    }
}
using BestStories.Api;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HackerNewsOptions>(
    builder.Configuration.GetSection("HackerNews"));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application services
builder.Services.AddSingleton<BestStoriesCache>();

builder.Services
    .AddHttpClient<IHackerNewsClient, HackerNewsClient>((serviceProvider, httpClient) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<HackerNewsOptions>>()
            .Value;

        httpClient.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddStandardResilienceHandler();

builder.Services.AddHostedService<BestStoriesRefresher>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Swagger
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Best Stories API v1");

    options.RoutePrefix = "swagger";
});

// Health check
app.MapHealthChecks("/health");

// Best stories endpoint
app.MapGet("/api/stories/best", async (
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

    httpContext.Response.Headers.CacheControl = "public, max-age=15";

    return Results.Ok(stories.Take(n.Value));
});

app.Run();

public partial class Program;
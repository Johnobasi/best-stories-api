using BestStories.Api;
using BestStories.Api.Endpoints;
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

// API endpoints
app.MapStoryEndpoints();
app.Run();

public partial class Program;
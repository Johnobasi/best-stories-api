# Hacker News Best Stories API

ASP.NET Core (.NET 8) REST API returning the best `n` Hacker News stories, ordered by score descending.

## Run

```bash
dotnet run --project src/BestStories.Api
curl "http://localhost:5000/api/stories/best?n=10"
```

Tests:

```bash
dotnet test
```

Docker:

```bash
docker build -t beststories .
docker run -p 8080:8080 beststories
```

## Endpoint

```http
GET /api/stories/best?n=10
```

Example response:

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

- `400` when `n` is missing, non-numeric, zero, or negative.
- `503` when the initial Hacker News snapshot is not available within the configured timeout.
- If `n` is greater than the number of available valid stories, all available valid stories are returned.
- `GET /health` provides a basic health endpoint.

## Design

A single `BackgroundService` (`BestStoriesRefresher`) is the only component that calls Hacker News.
It periodically retrieves the best-story IDs, fetches their details with bounded concurrency, sorts the
valid stories by score, and atomically publishes an in-memory snapshot.

The API request path only reads the latest snapshot and applies `Take(n)`. Therefore, client traffic does
not increase Hacker News traffic and there is no per-request cache stampede to manage.

### Why a background refresh?

The coding test requires the API to handle large numbers of requests without risking overload of the Hacker
News API. A background refresh gives predictable upstream traffic independent of client traffic while keeping
API latency low.

### Upstream request volume

Hacker News exposes the best-story IDs through `/v0/beststories`. The API documentation describes the story
lists as supporting up to 500 IDs. With the default configuration, one refresh therefore makes at most:

- 1 request for `beststories.json`
- 500 requests for individual `item/{id}.json` resources

So the theoretical maximum is 501 upstream requests per refresh, or approximately 501 requests per minute
with the default one-minute refresh interval. Actual traffic can be lower when fewer IDs are returned.

### Concurrency and resilience

- Individual story requests use bounded parallelism (`MaxConcurrency`, default 16).
- `IHttpClientFactory` manages the HTTP client lifecycle.
- The standard .NET HTTP resilience handler provides transient-failure handling.
- If a refresh fails, the last successful snapshot remains available.
- If an individual story refresh fails, its previous value is retained when one exists; a story with no
  previous value is omitted from that snapshot.

### Data mapping

The Hacker News `time` value is Unix time and is converted to `DateTimeOffset`.
`descendants` is mapped to `commentCount`.
An empty or missing Hacker News `url` is normalised to `null` in this API's response contract.
Deleted, dead, non-story items, and stories without a title are excluded.

## Configuration

```json
"HackerNews": {
  "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
  "RefreshInterval": "00:01:00",
  "InitialLoadTimeout": "00:00:10",
  "MaxConcurrency": 16,
  "BestStoryLimit": 500
}
```

`BestStoryLimit` is the maximum number of IDs processed from the Hacker News best-story list.
`RefreshInterval` is a freshness/performance trade-off and is configurable rather than hard-coded.

## Assumptions

- A single API instance owns its in-memory snapshot.
- Scores may be up to one refresh interval stale (60 seconds by default).
- The complete available best-story set is refreshed so the API can correctly determine the top `n` by score.
- `n` has no arbitrary application maximum; `Take(n)` naturally caps the result at the number of available stories.

## Enhancements with more time

- Multi-instance deployment: use a distributed/shared snapshot or a single refresh worker so every API instance
does not independently call Hacker News.
- Conditional/adaptive refresh based on the HN updates feed.
- Metrics for snapshot age, refresh duration, refresh failures, and upstream calls.
- A readiness endpoint that reports whether the snapshot is available and sufficiently fresh.
- Output caching/CDN support where appropriate.
- Integration tests against a stubbed Hacker News API.

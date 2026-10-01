using BestStories.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BestStories.Tests.TestFixtures
{

    public sealed class ApiTestFixture: WebApplicationFactory<Program>
    {
        public FakeHackerNewsClient Fake { get; } = new();

        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHackerNewsClient>();

                services.AddSingleton<IHackerNewsClient>(Fake);
            });
        }

        public new HttpClient CreateClient()
        {
            return base.CreateClient();
        }
    }
}
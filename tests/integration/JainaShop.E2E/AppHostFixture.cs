using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace JainaShop.E2E;

/// <summary>
/// xUnit class fixture that boots the full Aspire AppHost (Identity + Catalog + Orders +
/// Notifier + Gateway + Redis container) once per test class. Disposes when the class finishes.
/// Requires Docker daemon for the Redis resource.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.JainaShop_AppHost>();

        App = await builder.BuildAsync();
        await App.StartAsync();

        // Wait for every microservice to report Ready before tests run.
        var notifications = App.ResourceNotifications;
        await notifications.WaitForResourceHealthyAsync("identity").WaitAsync(TimeSpan.FromMinutes(2));
        await notifications.WaitForResourceHealthyAsync("orders").WaitAsync(TimeSpan.FromMinutes(2));
        await notifications.WaitForResourceHealthyAsync("gateway").WaitAsync(TimeSpan.FromMinutes(2));
    }

    public async Task DisposeAsync()
    {
        if (App is not null)
            await App.DisposeAsync();
    }
}

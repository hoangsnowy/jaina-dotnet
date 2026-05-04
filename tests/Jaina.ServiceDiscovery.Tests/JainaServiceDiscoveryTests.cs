using Jaina.ServiceDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.ServiceDiscovery;

namespace Jaina.ServiceDiscovery.Tests;

public class JainaServiceDiscoveryTests
{
    [Fact]
    public void AddJainaServiceDiscovery_RegistersServiceDiscoveryOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJainaServiceDiscovery();
        var opts = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;

        // Assert — options resolve, AddServiceDiscovery has wired them up
        Assert.NotNull(opts);
    }

    [Fact]
    public void AddJainaServiceDiscovery_AppliesConfigureCallback()
    {
        // Arrange
        var services = new ServiceCollection();
        var configured = false;

        // Act
        services.AddJainaServiceDiscovery(_ => configured = true);
        // IOptions are evaluated lazily — force evaluation
        var opts = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
        _ = opts;

        // Assert
        Assert.True(configured);
    }

    [Fact]
    public void AddJainaServiceDiscovery_IsIdempotent()
    {
        // Arrange / Act — registering twice should not throw
        var services = new ServiceCollection();
        services.AddJainaServiceDiscovery();
        services.AddJainaServiceDiscovery();

        // Assert
        var opts = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
        Assert.NotNull(opts);
    }
}

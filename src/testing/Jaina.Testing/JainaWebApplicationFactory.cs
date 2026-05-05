using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Testing;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> with a hook for replacing services in
/// the test fixture: <see cref="WithServices"/>. Supply an <see cref="Action{IServiceCollection}"/>
/// that swaps out infrastructure (e.g. EF InMemory provider, fake clock, fake user context).
/// </summary>
public class JainaWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private Action<IServiceCollection>? _configure;

    /// <summary>Replace or augment service registrations for the test fixture.</summary>
    public JainaWebApplicationFactory<TEntryPoint> WithServices(Action<IServiceCollection> configure)
    {
        _configure = configure;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services => _configure?.Invoke(services));
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Localization;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register a tenant-aware localizer that looks up <c>{tenantId}/{key}</c> first and
    /// falls back to the shared <c>{key}</c>. Each lookup emits an OTEL span via
    /// <c>JainaActivitySource</c> tagged with key, tenant, and which scope returned the
    /// result (<c>tenant</c> / <c>shared</c> / <c>missing</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// // Program.cs
    /// services.AddJainaMultiTenancy(b => b.FromHeader("X-Tenant"));
    /// services.AddJainaLocalization();    // resourcesPath defaults to "Resources"
    ///
    /// // Resources/CheckoutMessages.en.resx       OrderConfirmed = "Order confirmed."
    /// // Resources/CheckoutMessages.en.resx       acme/OrderConfirmed = "Your Acme order is on the way!"
    ///
    /// // Inject:
    /// public class CheckoutHandler(IJainaLocalizer&lt;CheckoutMessages&gt; t)
    /// {
    ///     public string Message(int orderId) => t["OrderConfirmed", orderId];
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddJainaLocalization(
        this IServiceCollection services,
        string resourcesPath = "Resources")
    {
        services.AddLocalization(o => o.ResourcesPath = resourcesPath);
        services.TryAddScoped(typeof(IJainaLocalizer<>), typeof(JainaLocalizer<>));
        return services;
    }
}

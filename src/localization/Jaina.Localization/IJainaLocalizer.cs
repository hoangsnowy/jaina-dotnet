using Microsoft.Extensions.Localization;

namespace Jaina.Localization;

/// <summary>
/// Tenant-aware localizer. Looks up <c>{tenantId}/{key}</c> first so tenants can override
/// shared strings (branded copy, regulated wording); falls back to the shared <c>{key}</c>
/// when no tenant-specific entry exists. Emits an OTEL span per lookup tagged with
/// <c>jaina.localization.tenant</c>, <c>jaina.localization.key</c>, and
/// <c>jaina.localization.found</c> so missing translations are immediately visible in
/// dashboards.
/// </summary>
public interface IJainaLocalizer<TResource>
{
    /// <summary>Lookup a string by key, with optional format args.</summary>
    LocalizedString this[string name] { get; }

    /// <summary>Lookup a string by key with format args; uses <c>string.Format</c> over the resolved template.</summary>
    LocalizedString this[string name, params object[] arguments] { get; }
}

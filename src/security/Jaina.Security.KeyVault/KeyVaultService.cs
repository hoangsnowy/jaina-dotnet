using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;

namespace Jaina.Security.KeyVault;

public class KeyVaultOptions
{
    public string VaultUri { get; set; } = "";
}

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _client;

    public KeyVaultService(IOptions<KeyVaultOptions> options)
    {
        _client = new SecretClient(new Uri(options.Value.VaultUri), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        var response = await _client.GetSecretAsync(secretName, cancellationToken: ct).ConfigureAwait(false);
        return response.Value.Value;
    }

    public string GetSecret(string secretName) =>
        _client.GetSecret(secretName).Value.Value;
}

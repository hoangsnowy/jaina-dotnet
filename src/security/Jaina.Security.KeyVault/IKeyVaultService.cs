namespace Jaina.Security.KeyVault;

public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName, CancellationToken ct = default);
    string GetSecret(string secretName);
}

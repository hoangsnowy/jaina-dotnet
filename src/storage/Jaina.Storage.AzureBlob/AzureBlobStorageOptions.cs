using System.ComponentModel.DataAnnotations;

namespace Jaina.Storage.AzureBlob;

public class AzureBlobStorageOptions
{
    [Required]
    public string ConnectionString { get; set; } = "";
    [Required]
    public string ContainerName { get; set; } = "";
}

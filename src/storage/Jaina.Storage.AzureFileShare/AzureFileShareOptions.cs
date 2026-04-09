using System.ComponentModel.DataAnnotations;

namespace Jaina.Storage.AzureFileShare;

public class AzureFileShareOptions
{
    [Required]
    public string ConnectionString { get; set; } = "";
    [Required]
    public string ShareName { get; set; } = "";
}

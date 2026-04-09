using System.ComponentModel.DataAnnotations;

namespace Jaina.Storage.Local;

public class LocalStorageOptions
{
    [Required]
    public string BasePath { get; set; } = "";
}

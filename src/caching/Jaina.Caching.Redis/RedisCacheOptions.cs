using System.ComponentModel.DataAnnotations;

namespace Jaina.Caching.Redis;

public class RedisCacheOptions
{
    [Required]
    public string ConnectionString { get; set; } = "";
    public string InstanceName { get; set; } = "";
}

using System.ComponentModel.DataAnnotations;

namespace Jaina.Messaging.RabbitMQ;

public class RabbitMQOptions
{
    [Required]
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? VirtualHost { get; set; }
    public string? ConnectionString { get; set; }
}

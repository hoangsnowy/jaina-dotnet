using System.ComponentModel.DataAnnotations;

namespace Jaina.Messaging.AzureServiceBus;

public class ServiceBusOptions
{
    [Required]
    public string ConnectionString { get; set; } = "";
}

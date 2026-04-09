using System.ComponentModel.DataAnnotations;

namespace Jaina.Security.Authentication.Client;

public class AuthorizationClientOptions
{
    [Required]
    public string ServiceUrl { get; set; } = "";
}

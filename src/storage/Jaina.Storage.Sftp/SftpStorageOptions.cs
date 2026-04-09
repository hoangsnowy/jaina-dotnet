using System.ComponentModel.DataAnnotations;

namespace Jaina.Storage.Sftp;

public class SftpStorageOptions
{
    [Required]
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    [Required]
    public string Username { get; set; } = "";
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassPhrase { get; set; }
    public string BasePath { get; set; } = "/";
}

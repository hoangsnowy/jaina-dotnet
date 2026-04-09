namespace Jaina.Security.Authentication;

public class AccessToken
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = "";
}

namespace Jaina.Security.Authentication;

public class IdentityUser
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public IList<string> Roles { get; set; } = new List<string>();
    public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
}

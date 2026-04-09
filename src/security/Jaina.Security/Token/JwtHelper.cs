using System.IdentityModel.Tokens.Jwt;

namespace Jaina.Security.Token;

public static class JwtHelper
{
    public static JwtSecurityToken ReadToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ReadJwtToken(token);
    }
}

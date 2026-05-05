using System.Security.Claims;
using Jaina.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jaina.MultiTenancy.Tests;

public class TenantResolverTests
{
    [Fact]
    public void Header_PresentValue_Resolves()
    {
        // Arrange
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant"] = "acme";

        // Act
        var t = new HeaderTenantResolver("X-Tenant").Resolve(ctx);

        // Assert
        Assert.NotNull(t);
        Assert.Equal("acme", t!.TenantId);
    }

    [Fact]
    public void Header_Missing_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();
        Assert.Null(new HeaderTenantResolver("X-Tenant").Resolve(ctx));
    }

    [Fact]
    public void Header_Whitespace_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant"] = "   ";
        Assert.Null(new HeaderTenantResolver("X-Tenant").Resolve(ctx));
    }

    [Fact]
    public void Claim_PresentValue_Resolves()
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tid", "globex") }));
        var t = new ClaimTenantResolver("tid").Resolve(ctx);
        Assert.NotNull(t);
        Assert.Equal("globex", t!.TenantId);
    }

    [Fact]
    public void Claim_Missing_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Null(new ClaimTenantResolver("tid").Resolve(ctx));
    }

    [Fact]
    public void Host_PatternMatches_Resolves()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("acme.api.example.com");
        var t = new HostTenantResolver(@"^([^.]+)\.api\.example\.com$").Resolve(ctx);
        Assert.NotNull(t);
        Assert.Equal("acme", t!.TenantId);
    }

    [Fact]
    public void Host_PatternMisses_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("api.different.com");
        Assert.Null(new HostTenantResolver(@"^([^.]+)\.api\.example\.com$").Resolve(ctx));
    }

    [Fact]
    public void Route_PresentValue_Resolves()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues = new RouteValueDictionary { ["tenant"] = "umbrella" };
        var t = new RouteTenantResolver("tenant").Resolve(ctx);
        Assert.NotNull(t);
        Assert.Equal("umbrella", t!.TenantId);
    }

    [Fact]
    public void Composite_FirstNonNullWins()
    {
        // Arrange — header missing, claim present
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tid", "from-claim") }));

        var resolver = new CompositeTenantResolver(new ITenantResolver[]
        {
            new HeaderTenantResolver("X-Tenant"),
            new ClaimTenantResolver("tid"),
        });

        // Act
        var t = resolver.Resolve(ctx);

        // Assert
        Assert.NotNull(t);
        Assert.Equal("from-claim", t!.TenantId);
    }

    [Fact]
    public void Composite_AllResolversReturnNull_ResolvesNull()
    {
        var ctx = new DefaultHttpContext();
        var resolver = new CompositeTenantResolver(new ITenantResolver[]
        {
            new HeaderTenantResolver("X-Tenant"),
            new ClaimTenantResolver("tid"),
        });
        Assert.Null(resolver.Resolve(ctx));
    }
}

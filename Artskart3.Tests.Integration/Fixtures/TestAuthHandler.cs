using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Fixtures;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Context.Request.Headers["X-Test-UserId"].FirstOrDefault();

        if (!Guid.TryParse(userId, out var parseUserGuid)) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim("sub", parseUserGuid.ToString()),
            new Claim("name", "Integration Test User"),
            new Claim("email", "integration-test@example.com")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
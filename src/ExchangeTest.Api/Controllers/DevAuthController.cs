using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/dev-auth")]
[AllowAnonymous]
public sealed class DevAuthController : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, Guid> MockUsers =
        new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ["B"] = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ["C"] = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ["D"] = Guid.Parse("44444444-4444-4444-4444-444444444444")
        };

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DevAuthController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("token/{userKey}")]
    public ActionResult<object> CreateToken(string userKey)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (!MockUsers.TryGetValue(userKey, out var userId))
            return BadRequest(new { message = "Use A, B, C or D." });

        var issuer = _configuration["Jwt:Issuer"]!;
        var audience = _configuration["Jwt:Audience"]!;
        var key = _configuration["Jwt:Key"]!;

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userKey.ToUpperInvariant())
            ],
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new
        {
            user = userKey.ToUpperInvariant(),
            userId,
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt
        });
    }
}

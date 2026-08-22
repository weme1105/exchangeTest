using System.Security.Claims;

namespace ExchangeTest.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User id claim is missing.");

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("User id claim is invalid.");

        return userId;
    }
}

using System.Security.Claims;

namespace JellyfinHideMedia.Tests.Helpers;

public static class Users
{
    public static ClaimsPrincipal Authenticated(Guid userId)
        => WithClaim(userId.ToString());

    public static ClaimsPrincipal WithClaim(string userIdClaim)
        => new(new ClaimsIdentity([new Claim("Jellyfin-UserId", userIdClaim)], "Test"));

    public static ClaimsPrincipal Anonymous { get; } = new(new ClaimsIdentity());
}

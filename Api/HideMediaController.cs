using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyfinHideMedia.Api;

[ApiController]
[Authorize]
[Route("HideMedia")]
public class HideMediaController : ControllerBase
{
    private readonly HiddenItemsStore _store;

    public HideMediaController(HiddenItemsStore store)
    {
        _store = store;
    }

    [HttpGet("Status/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HideStatus> GetStatus([FromRoute] Guid itemId)
    {
        if (User.GetJellyfinUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var target = _store.ResolveTarget(itemId);
        return new HideStatus(target is not null, target is { } t && _store.IsHidden(userId, t), target);
    }

    [HttpPost("Hidden/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<HideStatus> Hide([FromRoute] Guid itemId) => SetHidden(itemId, true);

    [HttpDelete("Hidden/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<HideStatus> Unhide([FromRoute] Guid itemId) => SetHidden(itemId, false);

    [HttpGet("Settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SectionSettings> GetSettings()
    {
        if (User.GetJellyfinUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return _store.GetSettings(userId);
    }

    [HttpPost("Settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SectionSettings> SaveSettings([FromBody] SectionSettings settings)
    {
        if (User.GetJellyfinUserId() is not { } userId)
        {
            return Unauthorized();
        }

        _store.SaveSettings(userId, settings);
        return settings;
    }

    private ActionResult<HideStatus> SetHidden(Guid itemId, bool hidden)
    {
        if (User.GetJellyfinUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (_store.ResolveTarget(itemId) is not { } target)
        {
            return BadRequest("Only movies, series, seasons and episodes can be hidden from Home.");
        }

        if (hidden)
        {
            _store.Hide(userId, target);
        }
        else
        {
            _store.Unhide(userId, target);
        }

        return new HideStatus(true, hidden, target);
    }
}

public record HideStatus(bool Eligible, bool Hidden, Guid? TargetId);

public static class ClaimsPrincipalExtensions
{
    private const string UserIdClaim = "Jellyfin-UserId";

    public static Guid? GetJellyfinUserId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(UserIdClaim), out var id) ? id : null;
}

using System.Security.Claims;
using JellyfinHideMedia.Api;
using JellyfinHideMedia.Tests.Helpers;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace JellyfinHideMedia.Tests;

public sealed class HideMediaControllerTests : IDisposable
{
    private readonly PluginHarness _harness = new();
    private readonly ILibraryManager _libraryManager = Substitute.For<ILibraryManager>();
    private readonly HiddenItemsStore _store;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _movie = Guid.NewGuid();
    private readonly Guid _series = Guid.NewGuid();
    private readonly Guid _season = Guid.NewGuid();
    private readonly Guid _episode = Guid.NewGuid();
    private readonly Guid _album = Guid.NewGuid();

    public HideMediaControllerTests()
    {
        _libraryManager.GetItemById(_movie).Returns(new Movie { Id = _movie });
        _libraryManager.GetItemById(_series).Returns(new Series { Id = _series });
        _libraryManager.GetItemById(_season).Returns(new Season { Id = _season, SeriesId = _series });
        _libraryManager.GetItemById(_episode).Returns(new Episode { Id = _episode, SeriesId = _series });
        _libraryManager.GetItemById(_album).Returns(new MusicAlbum { Id = _album });
        _store = new HiddenItemsStore(_libraryManager);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Hide_Movie_HidesIt()
    {
        var result = Controller().Hide(_movie);

        Assert.Equal(new HideStatus(true, true, _movie), result.Value);
        Assert.True(_store.IsHidden(_user, _movie));
    }

    [Fact]
    public void Hide_Episode_HidesTheWholeSeries()
    {
        var result = Controller().Hide(_episode);

        Assert.Equal(new HideStatus(true, true, _series), result.Value);
        Assert.True(_store.IsHidden(_user, _series));
        Assert.False(_store.IsHidden(_user, _episode));
    }

    [Fact]
    public void Unhide_Season_UnhidesTheSeriesHiddenFromAnEpisode()
    {
        Controller().Hide(_episode);

        var result = Controller().Unhide(_season);

        Assert.Equal(new HideStatus(true, false, _series), result.Value);
        Assert.False(_store.IsHidden(_user, _series));
    }

    [Fact]
    public void Hide_IneligibleItem_IsRejectedAndNothingIsStored()
    {
        var result = Controller().Hide(_album);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_harness.Plugin.Configuration.HiddenItems);
    }

    [Fact]
    public void Unhide_IneligibleItem_IsRejected()
        => Assert.IsType<BadRequestObjectResult>(Controller().Unhide(Guid.NewGuid()).Result);

    [Fact]
    public void GetStatus_ReflectsHiddenStateOfTheResolvedTarget()
    {
        Assert.Equal(new HideStatus(true, false, _series), Controller().GetStatus(_episode).Value);

        Controller().Hide(_series);

        Assert.Equal(new HideStatus(true, true, _series), Controller().GetStatus(_episode).Value);
        Assert.Equal(new HideStatus(true, true, _series), Controller().GetStatus(_season).Value);
        Assert.Equal(new HideStatus(true, false, _movie), Controller().GetStatus(_movie).Value);
    }

    [Fact]
    public void GetStatus_IneligibleItem_IsNotEligible()
        => Assert.Equal(new HideStatus(false, false, null), Controller().GetStatus(_album).Value);

    [Fact]
    public void GetStatus_IsPerUser()
    {
        Controller().Hide(_movie);

        Assert.Equal(new HideStatus(true, false, _movie), Controller(Users.Authenticated(Guid.NewGuid())).GetStatus(_movie).Value);
    }

    [Fact]
    public void Settings_DefaultToAllSections()
        => Assert.Equal(new SectionSettings(true, true, true), Controller().GetSettings().Value);

    [Fact]
    public void SaveSettings_IsStoredForTheCallingUser()
    {
        var settings = new SectionSettings(RecentlyAdded: false, ContinueWatching: true, NextUp: false);

        var saved = Controller().SaveSettings(settings);

        Assert.Equal(settings, saved.Value);
        Assert.Equal(settings, Controller().GetSettings().Value);
        Assert.Equal(new SectionSettings(true, true, true), Controller(Users.Authenticated(Guid.NewGuid())).GetSettings().Value);
    }

    public static TheoryData<string> UnauthenticatedUsers => ["anonymous", "malformed"];

    [Theory]
    [MemberData(nameof(UnauthenticatedUsers))]
    public void EveryEndpoint_RequiresAJellyfinUserId(string kind)
    {
        var controller = Controller(kind == "anonymous" ? Users.Anonymous : Users.WithClaim("not-a-guid"));

        Assert.IsType<UnauthorizedResult>(controller.GetStatus(_movie).Result);
        Assert.IsType<UnauthorizedResult>(controller.Hide(_movie).Result);
        Assert.IsType<UnauthorizedResult>(controller.Unhide(_movie).Result);
        Assert.IsType<UnauthorizedResult>(controller.GetSettings().Result);
        Assert.IsType<UnauthorizedResult>(controller.SaveSettings(new SectionSettings(false, false, false)).Result);
        Assert.Empty(_harness.Plugin.Configuration.HiddenItems);
    }

    private HideMediaController Controller(ClaimsPrincipal? user = null)
        => new(_store)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user ?? Users.Authenticated(_user) }
            }
        };
}

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void ReadsTheJellyfinUserIdClaim()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, Users.Authenticated(id).GetJellyfinUserId());
        Assert.Equal(id, Users.WithClaim(id.ToString("N")).GetJellyfinUserId());
    }

    [Fact]
    public void MissingOrInvalidClaim_IsNull()
    {
        Assert.Null(Users.Anonymous.GetJellyfinUserId());
        Assert.Null(Users.WithClaim("").GetJellyfinUserId());
        Assert.Null(Users.WithClaim("abc").GetJellyfinUserId());
        Assert.Null(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())])).GetJellyfinUserId());
    }
}

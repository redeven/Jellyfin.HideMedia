using System.Security.Claims;
using JellyfinHideMedia.Tests.Helpers;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace JellyfinHideMedia.Tests;

public sealed class HomeSectionsFilterTests : IDisposable
{
    private readonly PluginHarness _harness = new();
    private readonly HiddenItemsStore _store = new(Substitute.For<ILibraryManager>());
    private readonly HomeSectionsFilter _filter;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _hiddenMovie = Guid.NewGuid();
    private readonly Guid _hiddenSeries = Guid.NewGuid();

    public HomeSectionsFilterTests()
    {
        _filter = new HomeSectionsFilter(_store);
        _store.Hide(_user, _hiddenMovie);
        _store.Hide(_user, _hiddenSeries);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task RecentlyAdded_RemovesHiddenMoviesAndEpisodesOfHiddenSeries()
    {
        var visibleMovie = Item();
        var visibleEpisode = Item(seriesId: Guid.NewGuid());
        BaseItemDto[] items = [Item(_hiddenMovie), visibleMovie, Item(seriesId: _hiddenSeries), visibleEpisode, Item(_hiddenSeries)];

        var result = await Run("UserLibrary", "GetLatestMedia", items);

        Assert.Equal([visibleMovie, visibleEpisode], Assert.IsAssignableFrom<IEnumerable<BaseItemDto>>(result));
    }

    [Theory]
    [InlineData("Items", "GetResumeItems")]
    [InlineData("TvShows", "GetNextUp")]
    public async Task PagedSections_RemoveHiddenItemsAndAdjustTheTotal(string controller, string action)
    {
        var visible = Item(seriesId: Guid.NewGuid());
        var query = new QueryResult<BaseItemDto>(5, 20, [Item(seriesId: _hiddenSeries), visible, Item(_hiddenMovie)]);

        var result = Assert.IsType<QueryResult<BaseItemDto>>(await Run(controller, action, query));

        Assert.Equal([visible], result.Items);
        Assert.Equal(5, result.StartIndex);
        Assert.Equal(18, result.TotalRecordCount);
    }

    [Fact]
    public async Task PagedSection_TotalNeverGoesNegative()
    {
        var query = new QueryResult<BaseItemDto>(0, 1, [Item(_hiddenMovie), Item(seriesId: _hiddenSeries)]);

        var result = Assert.IsType<QueryResult<BaseItemDto>>(await Run("Items", "GetResumeItems", query));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalRecordCount);
    }

    [Theory]
    [InlineData("UserLibrary", "GetLatestMedia", HomeSection.RecentlyAdded)]
    [InlineData("Items", "GetResumeItems", HomeSection.ContinueWatching)]
    [InlineData("TvShows", "GetNextUp", HomeSection.NextUp)]
    public async Task EachEndpoint_FollowsOnlyItsOwnSectionSetting(string controller, string action, HomeSection section)
    {
        var query = new QueryResult<BaseItemDto>([Item(_hiddenMovie), Item()]);

        _store.SaveSettings(_user, new SectionSettings(
            RecentlyAdded: section != HomeSection.RecentlyAdded,
            ContinueWatching: section != HomeSection.ContinueWatching,
            NextUp: section != HomeSection.NextUp));
        Assert.Same(query, await Run(controller, action, query));

        _store.SaveSettings(_user, new SectionSettings(
            RecentlyAdded: section == HomeSection.RecentlyAdded,
            ContinueWatching: section == HomeSection.ContinueWatching,
            NextUp: section == HomeSection.NextUp));
        Assert.Single(Assert.IsType<QueryResult<BaseItemDto>>(await Run(controller, action, query)).Items);
    }

    [Theory]
    [InlineData("Items", "GetItems")]
    [InlineData("UserLibrary", "GetItem")]
    [InlineData("TvShows", "GetEpisodes")]
    [InlineData("Search", "GetSearchHints")]
    public async Task LibraryBrowsingEndpoints_AreNotFiltered(string controller, string action)
    {
        var query = new QueryResult<BaseItemDto>([Item(_hiddenMovie), Item(seriesId: _hiddenSeries)]);

        Assert.Same(query, await Run(controller, action, query));
    }

    [Fact]
    public async Task OtherUsers_SeeEverything()
    {
        BaseItemDto[] items = [Item(_hiddenMovie), Item(seriesId: _hiddenSeries)];

        Assert.Same(items, await Run("UserLibrary", "GetLatestMedia", items, Users.Authenticated(Guid.NewGuid())));
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsLeftAlone()
    {
        BaseItemDto[] items = [Item(_hiddenMovie)];

        Assert.Same(items, await Run("UserLibrary", "GetLatestMedia", items, Users.Anonymous));
    }

    [Fact]
    public async Task UnrecognisedResultValue_IsLeftAlone()
    {
        var value = new { Id = _hiddenMovie };

        Assert.Same(value, await Run("UserLibrary", "GetLatestMedia", value));
    }

    [Fact]
    public async Task NonObjectResult_StillContinuesThePipeline()
    {
        var context = Context("UserLibrary", "GetLatestMedia", new NotFoundResult(), Users.Authenticated(_user));
        var calls = 0;

        await _filter.OnResultExecutionAsync(context, Next(context, () => calls++));

        Assert.Equal(1, calls);
        Assert.IsType<NotFoundResult>(context.Result);
    }

    [Fact]
    public async Task NonControllerAction_IsLeftAlone()
    {
        BaseItemDto[] items = [Item(_hiddenMovie)];
        var actionContext = new ActionContext(
            new DefaultHttpContext { User = Users.Authenticated(_user) },
            new RouteData(),
            new ActionDescriptor());
        var context = new ResultExecutingContext(actionContext, [], new ObjectResult(items), new object());

        await _filter.OnResultExecutionAsync(context, Next(context));

        Assert.Same(items, ((ObjectResult)context.Result).Value);
    }

    private async Task<object?> Run(string controller, string action, object value, ClaimsPrincipal? user = null)
    {
        var context = Context(controller, action, new ObjectResult(value), user ?? Users.Authenticated(_user));
        var calls = 0;

        await _filter.OnResultExecutionAsync(context, Next(context, () => calls++));

        Assert.Equal(1, calls);
        return ((ObjectResult)context.Result).Value;
    }

    private static ResultExecutingContext Context(string controller, string action, IActionResult result, ClaimsPrincipal user)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext { User = user },
            new RouteData(),
            new ControllerActionDescriptor { ControllerName = controller, ActionName = action });
        return new ResultExecutingContext(actionContext, [], result, new object());
    }

    private static ResultExecutionDelegate Next(ResultExecutingContext context, Action? onCalled = null)
        => () =>
        {
            onCalled?.Invoke();
            return Task.FromResult(new ResultExecutedContext(context, context.Filters, context.Result, context.Controller));
        };

    private static BaseItemDto Item(Guid? id = null, Guid? seriesId = null)
        => new() { Id = id ?? Guid.NewGuid(), SeriesId = seriesId };
}

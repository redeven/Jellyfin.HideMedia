using System.Security.Claims;
using JellyfinHideMedia.Tests.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
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

public sealed class HiddenLibraryFilterTests : IDisposable
{
    private readonly PluginHarness _harness = new();
    private readonly ILibraryManager _libraryManager = Substitute.For<ILibraryManager>();
    private readonly HiddenItemsStore _store;
    private readonly HiddenLibraryFilter _filter;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _hiddenMovie = Guid.NewGuid();
    private readonly Guid _hiddenSeries = Guid.NewGuid();
    private readonly Guid _moviesLibrary = Guid.NewGuid();
    private readonly Guid _showsLibrary = Guid.NewGuid();
    private readonly Guid _showsSubfolder = Guid.NewGuid();

    public HiddenLibraryFilterTests()
    {
        _store = new HiddenItemsStore(_libraryManager);
        _filter = new HiddenLibraryFilter(_store, _libraryManager);
        _store.Hide(_user, _hiddenMovie);
        _store.Hide(_user, _hiddenSeries);

        var subfolder = new Folder { Id = _showsSubfolder };
        var movie = new Movie { Id = _hiddenMovie };
        var series = new Series { Id = _hiddenSeries, ParentId = _showsSubfolder };
        _libraryManager.GetItemById(_showsSubfolder).Returns(subfolder);
        _libraryManager.GetItemById(_hiddenMovie).Returns(movie);
        _libraryManager.GetItemById(_hiddenSeries).Returns(series);
        _libraryManager.GetCollectionFolders(movie).Returns([new Folder { Id = _moviesLibrary }]);
        _libraryManager.GetCollectionFolders(series).Returns([new Folder { Id = _showsLibrary }]);
        BaseItem.LibraryManager = _libraryManager;
    }

    public void Dispose()
    {
        BaseItem.LibraryManager = null!;
        _harness.Dispose();
    }

    [Theory]
    [InlineData("GetItems")]
    [InlineData("GetItemsByUserIdLegacy")]
    public async Task TaggedRequest_IsRestrictedToHiddenItems(string action)
    {
        var context = await Run("Items", action, "?hideMediaHidden=true&parentId=abc");

        Assert.Null(context.Result);
        Assert.Equal(
            new HashSet<Guid> { _hiddenMovie, _hiddenSeries },
            Assert.IsType<Guid[]>(context.ActionArguments["ids"]).ToHashSet());
    }

    [Fact]
    public async Task TaggedRequestWithParent_KeepsOnlyHiddenItemsInThatLibrary()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", parentId: _showsLibrary);

        Assert.Equal([_hiddenSeries], Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Fact]
    public async Task TaggedRequestWithParentFolder_KeepsHiddenItemsBelowIt()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", parentId: _showsSubfolder);

        Assert.Equal([_hiddenSeries], Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Fact]
    public async Task TaggedRequestWithParent_WithNothingHiddenThere_ReturnsNothing()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", parentId: Guid.NewGuid(), expectNext: false);

        AssertEmptyResult(context);
    }

    [Fact]
    public async Task TaggedRequestWithParent_DropsHiddenItemsThatNoLongerExist()
    {
        var removed = Guid.NewGuid();
        _store.Hide(_user, removed);

        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", parentId: _moviesLibrary);

        Assert.Equal([_hiddenMovie], Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Fact]
    public async Task TaggedRequestWithIds_KeepsOnlyRequestedHiddenItems()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", [_hiddenMovie, Guid.NewGuid()]);

        Assert.Equal([_hiddenMovie], Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Fact]
    public async Task TaggedRequestWithIdsThatAreNotHidden_ReturnsNothing()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", [Guid.NewGuid()], expectNext: false);

        AssertEmptyResult(context);
    }

    [Fact]
    public async Task TaggedRequest_WithNothingHidden_ReturnsNothing()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", user: Users.Authenticated(Guid.NewGuid()), expectNext: false);

        AssertEmptyResult(context);
    }

    [Fact]
    public async Task TaggedRequest_IgnoresHomeSectionSettings()
    {
        _store.SaveSettings(_user, new SectionSettings(false, false, false));

        var context = await Run("Items", "GetItems", "?hideMediaHidden=true");

        Assert.Equal(2, Assert.IsType<Guid[]>(context.ActionArguments["ids"]).Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?hideMediaHidden=false")]
    [InlineData("?parentId=abc")]
    public async Task UntaggedRequest_IsLeftAlone(string query)
    {
        var context = await Run("Items", "GetItems", query);

        Assert.Empty(Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Theory]
    [InlineData("Items", "GetResumeItems")]
    [InlineData("UserLibrary", "GetLatestMedia")]
    [InlineData("Items", "GetQueryFilters")]
    public async Task OtherEndpoints_AreLeftAlone(string controller, string action)
    {
        var context = await Run(controller, action, "?hideMediaHidden=true");

        Assert.Empty(Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsLeftAlone()
    {
        var context = await Run("Items", "GetItems", "?hideMediaHidden=true", user: Users.Anonymous);

        Assert.Empty(Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    [Fact]
    public async Task NonControllerAction_IsLeftAlone()
    {
        var actionContext = new ActionContext(
            Http("?hideMediaHidden=true", Users.Authenticated(_user)),
            new RouteData(),
            new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?> { ["ids"] = Array.Empty<Guid>() }, new object());
        var calls = 0;

        await _filter.OnActionExecutionAsync(context, Next(context, () => calls++));

        Assert.Equal(1, calls);
        Assert.Empty(Assert.IsType<Guid[]>(context.ActionArguments["ids"]));
    }

    private async Task<ActionExecutingContext> Run(
        string controller,
        string action,
        string query,
        Guid[]? ids = null,
        Guid? parentId = null,
        ClaimsPrincipal? user = null,
        bool expectNext = true)
    {
        var actionContext = new ActionContext(
            Http(query, user ?? Users.Authenticated(_user)),
            new RouteData(),
            new ControllerActionDescriptor { ControllerName = controller, ActionName = action });
        var arguments = new Dictionary<string, object?> { ["ids"] = ids ?? [], ["parentId"] = parentId };
        var context = new ActionExecutingContext(actionContext, [], arguments, new object());
        var calls = 0;

        await _filter.OnActionExecutionAsync(context, Next(context, () => calls++));

        Assert.Equal(expectNext ? 1 : 0, calls);
        return context;
    }

    private static void AssertEmptyResult(ActionExecutingContext context)
    {
        var result = Assert.IsType<QueryResult<BaseItemDto>>(Assert.IsType<OkObjectResult>(context.Result).Value);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalRecordCount);
    }

    private static DefaultHttpContext Http(string query, ClaimsPrincipal user)
    {
        var http = new DefaultHttpContext { User = user };
        http.Request.QueryString = new QueryString(query);
        return http;
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, Action onCalled)
        => () =>
        {
            onCalled();
            return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
        };
}

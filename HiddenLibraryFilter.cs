using JellyfinHideMedia.Api;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JellyfinHideMedia;

public class HiddenLibraryFilter : IAsyncActionFilter
{
    public const string QueryParameter = "hideMediaHidden";

    private static readonly HashSet<string> FilteredActions =
    [
        "Items.GetItems",
        "Items.GetItemsByUserIdLegacy"
    ];

    private readonly HiddenItemsStore _store;
    private readonly ILibraryManager _libraryManager;

    public HiddenLibraryFilter(HiddenItemsStore store, ILibraryManager libraryManager)
    {
        _store = store;
        _libraryManager = libraryManager;
    }

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action
            && FilteredActions.Contains($"{action.ControllerName}.{action.ActionName}")
            && context.HttpContext.Request.Query[QueryParameter] == "true"
            && context.HttpContext.User.GetJellyfinUserId() is { } userId)
        {
            var hidden = _store.GetAllHidden(userId);
            if (context.ActionArguments.TryGetValue("ids", out var value) && value is Guid[] { Length: > 0 } ids)
            {
                hidden.IntersectWith(ids);
            }

            if (context.ActionArguments.TryGetValue("parentId", out var parent) && parent is Guid parentId && parentId != Guid.Empty)
            {
                hidden.RemoveWhere(id => !IsInside(id, parentId));
            }

            if (hidden.Count == 0)
            {
                context.Result = new OkObjectResult(new QueryResult<BaseItemDto>());
                return Task.CompletedTask;
            }

            context.ActionArguments["ids"] = hidden.ToArray();
        }

        return next();
    }

    private bool IsInside(Guid itemId, Guid parentId)
        => _libraryManager.GetItemById(itemId) is { } item
            && (_libraryManager.GetCollectionFolders(item).Any(f => f.Id == parentId)
                || item.GetParents().Any(p => p.Id == parentId));
}

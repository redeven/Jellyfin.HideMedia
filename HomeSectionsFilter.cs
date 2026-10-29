using JellyfinHideMedia.Api;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JellyfinHideMedia;

public class HomeSectionsFilter : IAsyncResultFilter
{
    private static readonly Dictionary<string, HomeSection> FilteredActions = new()
    {
        ["UserLibrary.GetLatestMedia"] = HomeSection.RecentlyAdded,
        ["Items.GetResumeItems"] = HomeSection.ContinueWatching,
        ["TvShows.GetNextUp"] = HomeSection.NextUp
    };

    private readonly HiddenItemsStore _store;

    public HomeSectionsFilter(HiddenItemsStore store)
    {
        _store = store;
    }

    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action
            && FilteredActions.TryGetValue($"{action.ControllerName}.{action.ActionName}", out var section)
            && context.Result is ObjectResult result
            && context.HttpContext.User.GetJellyfinUserId() is { } userId)
        {
            var hidden = _store.GetHidden(userId, section);
            if (hidden.Count > 0)
            {
                result.Value = result.Value switch
                {
                    QueryResult<BaseItemDto> query => Filter(query, hidden),
                    IEnumerable<BaseItemDto> items => items.Where(i => !IsHidden(i, hidden)).ToArray(),
                    var other => other
                };
            }
        }

        return next();
    }

    private static QueryResult<BaseItemDto> Filter(QueryResult<BaseItemDto> query, HashSet<Guid> hidden)
    {
        var kept = query.Items.Where(i => !IsHidden(i, hidden)).ToArray();
        var removed = query.Items.Count - kept.Length;
        return new QueryResult<BaseItemDto>(query.StartIndex, Math.Max(0, query.TotalRecordCount - removed), kept);
    }

    private static bool IsHidden(BaseItemDto item, HashSet<Guid> hidden)
        => hidden.Contains(item.Id) || (item.SeriesId is { } seriesId && hidden.Contains(seriesId));
}

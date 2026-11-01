using JellyfinHideMedia.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace JellyfinHideMedia;

public class HiddenItemsStore
{
    private readonly ILibraryManager _libraryManager;
    private readonly Lock _lock = new();

    public HiddenItemsStore(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    private static Plugin Plugin => Plugin.Instance ?? throw new InvalidOperationException("Plugin is not initialized.");

    public HashSet<Guid> GetHidden(Guid userId, HomeSection section)
    {
        lock (_lock)
        {
            var entry = Find(userId);
            return entry is not null && GetSettings(entry).AppliesTo(section) ? [.. entry.ItemIds] : [];
        }
    }

    public HashSet<Guid> GetAllHidden(Guid userId)
    {
        lock (_lock)
        {
            return Find(userId) is { } entry ? [.. entry.ItemIds] : [];
        }
    }

    public SectionSettings GetSettings(Guid userId)
    {
        lock (_lock)
        {
            return GetSettings(Find(userId) ?? new UserHiddenItems());
        }
    }

    public void SaveSettings(Guid userId, SectionSettings settings)
    {
        lock (_lock)
        {
            var entry = GetOrAdd(userId);
            entry.HideFromRecentlyAdded = settings.RecentlyAdded;
            entry.HideFromContinueWatching = settings.ContinueWatching;
            entry.HideFromNextUp = settings.NextUp;
            Plugin.SaveConfiguration();
        }
    }

    public bool IsHidden(Guid userId, Guid itemId)
    {
        lock (_lock)
        {
            return Find(userId)?.ItemIds.Contains(itemId) ?? false;
        }
    }

    public void Hide(Guid userId, Guid itemId)
    {
        lock (_lock)
        {
            var entry = GetOrAdd(userId);
            if (entry.ItemIds.Contains(itemId))
            {
                return;
            }

            entry.ItemIds.Add(itemId);
            Plugin.SaveConfiguration();
        }
    }

    public void Unhide(Guid userId, Guid itemId)
    {
        lock (_lock)
        {
            if (Find(userId)?.ItemIds.Remove(itemId) == true)
            {
                Plugin.SaveConfiguration();
            }
        }
    }

    public Guid? ResolveTarget(Guid itemId)
    {
        return _libraryManager.GetItemById(itemId) switch
        {
            Movie or Series => itemId,
            Episode episode when episode.SeriesId != Guid.Empty => episode.SeriesId,
            Season season when season.SeriesId != Guid.Empty => season.SeriesId,
            _ => null
        };
    }

    private static UserHiddenItems? Find(Guid userId)
        => Plugin.Configuration.HiddenItems.Find(e => e.UserId == userId);

    private static UserHiddenItems GetOrAdd(Guid userId)
    {
        var entry = Find(userId);
        if (entry is null)
        {
            entry = new UserHiddenItems { UserId = userId };
            Plugin.Configuration.HiddenItems.Add(entry);
        }

        return entry;
    }

    private static SectionSettings GetSettings(UserHiddenItems entry)
        => new(entry.HideFromRecentlyAdded, entry.HideFromContinueWatching, entry.HideFromNextUp);
}

public enum HomeSection
{
    RecentlyAdded,
    ContinueWatching,
    NextUp
}

public record SectionSettings(bool RecentlyAdded, bool ContinueWatching, bool NextUp)
{
    public bool AppliesTo(HomeSection section) => section switch
    {
        HomeSection.RecentlyAdded => RecentlyAdded,
        HomeSection.ContinueWatching => ContinueWatching,
        HomeSection.NextUp => NextUp,
        _ => false
    };
}

using MediaBrowser.Model.Plugins;

namespace JellyfinHideMedia.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public List<UserHiddenItems> HiddenItems { get; set; } = [];
}

public class UserHiddenItems
{
    public Guid UserId { get; set; }

    public List<Guid> ItemIds { get; set; } = [];

    public bool HideFromRecentlyAdded { get; set; } = true;

    public bool HideFromContinueWatching { get; set; } = true;

    public bool HideFromNextUp { get; set; } = true;
}

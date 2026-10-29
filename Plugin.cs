using JellyfinHideMedia.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Serialization;

namespace JellyfinHideMedia;

public class Plugin : BasePlugin<PluginConfiguration>, IHasEmbeddedImage
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Hide Media";

    public override Guid Id => Guid.Parse("b3a22fa0-4af7-490e-9cdf-420510886ad1");

    public string ImageResourceName => "JellyfinHideMedia.Images.hide-media.png";

    public override void OnUninstalling()
    {
        JavaScriptInjectorBridge.UnregisterAllScripts(Id.ToString());
        base.OnUninstalling();
    }
}

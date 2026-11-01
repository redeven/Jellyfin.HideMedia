using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace JellyfinHideMedia;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<HiddenItemsStore>();
        serviceCollection.AddHostedService<ScriptRegistration>();
        serviceCollection.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<HomeSectionsFilter>();
            options.Filters.Add<HiddenLibraryFilter>();
        });
    }
}

using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace JellyfinHideMedia.Tests;

public class PluginServiceRegistratorTests
{
    private readonly ServiceProvider _services;

    public PluginServiceRegistratorTests()
    {
        var collection = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Substitute.For<ILibraryManager>());
        new PluginServiceRegistrator().RegisterServices(collection, Substitute.For<IServerApplicationHost>());
        _services = collection.BuildServiceProvider();
    }

    [Fact]
    public void Store_IsASingleton()
        => Assert.Same(_services.GetRequiredService<HiddenItemsStore>(), _services.GetRequiredService<HiddenItemsStore>());

    [Fact]
    public void ScriptRegistration_RunsAsAHostedService()
        => Assert.Contains(_services.GetServices<IHostedService>(), s => s is ScriptRegistration);

    [Fact]
    public void HomeSectionsFilter_IsAddedGloballyAndResolvable()
    {
        var filter = Assert.Single(
            _services.GetRequiredService<IOptions<MvcOptions>>().Value.Filters.OfType<TypeFilterAttribute>(),
            f => f.ImplementationType == typeof(HomeSectionsFilter));

        Assert.IsType<HomeSectionsFilter>(filter.CreateInstance(_services));
    }
}

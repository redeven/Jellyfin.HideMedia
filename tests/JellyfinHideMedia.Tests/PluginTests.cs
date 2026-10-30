using JellyfinHideMedia.Tests.Helpers;

namespace JellyfinHideMedia.Tests;

public sealed class PluginTests : IDisposable
{
    private readonly PluginHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void IdMatchesTheRepositoryManifest()
        => Assert.Equal(Guid.Parse("b3a22fa0-4af7-490e-9cdf-420510886ad1"), _harness.Plugin.Id);

    [Fact]
    public void ConstructingThePlugin_PublishesItAsTheInstance()
        => Assert.Same(_harness.Plugin, Plugin.Instance);

    [Fact]
    public void EmbeddedImage_Exists()
    {
        using var image = typeof(Plugin).Assembly.GetManifestResourceStream(_harness.Plugin.ImageResourceName);

        Assert.NotNull(image);
        Assert.True(image.Length > 0);
    }

    [Fact]
    public void Uninstalling_RemovesAllOfThePluginsScripts()
    {
        using var injector = new FakeJavaScriptInjector();

        _harness.Plugin.OnUninstalling();

        Assert.Equal([$"UnregisterAll:{_harness.Plugin.Id}"], injector.Calls);
    }

    [Fact]
    public void Uninstalling_WithoutInjector_DoesNotThrow()
        => _harness.Plugin.OnUninstalling();
}

using JellyfinHideMedia.Tests.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using NSubstitute;

namespace JellyfinHideMedia.Tests;

public sealed class HiddenItemsStoreTests : IDisposable
{
    private static readonly HomeSection[] AllSections = Enum.GetValues<HomeSection>();

    private readonly PluginHarness _harness = new();
    private readonly ILibraryManager _libraryManager = Substitute.For<ILibraryManager>();
    private readonly HiddenItemsStore _store;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();

    public HiddenItemsStoreTests()
    {
        _store = new HiddenItemsStore(_libraryManager);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void NewUser_HasNothingHiddenAndAllSectionsEnabled()
    {
        Assert.All(AllSections, section => Assert.Empty(_store.GetHidden(_user, section)));
        Assert.Equal(new SectionSettings(true, true, true), _store.GetSettings(_user));
    }

    [Fact]
    public void ReadingANewUser_DoesNotCreateOrPersistAnEntry()
    {
        _store.GetHidden(_user, HomeSection.NextUp);
        _store.GetSettings(_user);
        _store.IsHidden(_user, Guid.NewGuid());

        Assert.Empty(_harness.Plugin.Configuration.HiddenItems);
        Assert.Equal(0, _harness.Saves);
    }

    [Fact]
    public void Hide_HidesItemFromEverySectionByDefault()
    {
        var item = Guid.NewGuid();

        _store.Hide(_user, item);

        Assert.True(_store.IsHidden(_user, item));
        Assert.All(AllSections, section => Assert.Equal([item], _store.GetHidden(_user, section)));
        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public void Hide_OnlyAffectsTheUserWhoHidIt()
    {
        var item = Guid.NewGuid();

        _store.Hide(_user, item);

        Assert.False(_store.IsHidden(_otherUser, item));
        Assert.Empty(_store.GetHidden(_otherUser, HomeSection.RecentlyAdded));
    }

    [Fact]
    public void Hide_SameItemTwice_StoresItOnceAndSavesOnce()
    {
        var item = Guid.NewGuid();

        _store.Hide(_user, item);
        _store.Hide(_user, item);

        Assert.Equal([item], _harness.Plugin.Configuration.HiddenItems.Single().ItemIds);
        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public void Unhide_RemovesOnlyThatItem()
    {
        var kept = Guid.NewGuid();
        var removed = Guid.NewGuid();
        _store.Hide(_user, kept);
        _store.Hide(_user, removed);

        _store.Unhide(_user, removed);

        Assert.False(_store.IsHidden(_user, removed));
        Assert.Equal([kept], _store.GetHidden(_user, HomeSection.ContinueWatching));
        Assert.Equal(3, _harness.Saves);
    }

    [Fact]
    public void Unhide_ItemThatIsNotHidden_DoesNotSave()
    {
        _store.Unhide(_user, Guid.NewGuid());
        _store.Hide(_user, Guid.NewGuid());
        _store.Unhide(_user, Guid.NewGuid());

        Assert.Equal(1, _harness.Saves);
    }

    [Fact]
    public void Unhide_DoesNotTouchOtherUsers()
    {
        var item = Guid.NewGuid();
        _store.Hide(_user, item);
        _store.Hide(_otherUser, item);

        _store.Unhide(_user, item);

        Assert.True(_store.IsHidden(_otherUser, item));
    }

    [Fact]
    public void SaveSettings_DisabledSectionStopsFilteringOnlyThatSection()
    {
        var item = Guid.NewGuid();
        _store.Hide(_user, item);

        _store.SaveSettings(_user, new SectionSettings(RecentlyAdded: true, ContinueWatching: false, NextUp: true));

        Assert.Equal([item], _store.GetHidden(_user, HomeSection.RecentlyAdded));
        Assert.Empty(_store.GetHidden(_user, HomeSection.ContinueWatching));
        Assert.Equal([item], _store.GetHidden(_user, HomeSection.NextUp));
        Assert.True(_store.IsHidden(_user, item));
    }

    [Fact]
    public void SaveSettings_ForNewUser_IsRememberedBeforeAnythingIsHidden()
    {
        var settings = new SectionSettings(RecentlyAdded: false, ContinueWatching: true, NextUp: false);

        _store.SaveSettings(_user, settings);
        var item = Guid.NewGuid();
        _store.Hide(_user, item);

        Assert.Equal(settings, _store.GetSettings(_user));
        Assert.Empty(_store.GetHidden(_user, HomeSection.RecentlyAdded));
        Assert.Equal([item], _store.GetHidden(_user, HomeSection.ContinueWatching));
        Assert.Equal(new SectionSettings(true, true, true), _store.GetSettings(_otherUser));
    }

    [Fact]
    public void HiddenItemsAndSettings_SurviveAServerRestart()
    {
        var item = Guid.NewGuid();
        var settings = new SectionSettings(RecentlyAdded: true, ContinueWatching: false, NextUp: true);
        _store.Hide(_user, item);
        _store.SaveSettings(_user, settings);

        _harness.Restart();
        var reloaded = new HiddenItemsStore(_libraryManager);

        Assert.True(reloaded.IsHidden(_user, item));
        Assert.Equal(settings, reloaded.GetSettings(_user));
        Assert.Equal([item], reloaded.GetHidden(_user, HomeSection.NextUp));
        Assert.Empty(reloaded.GetHidden(_user, HomeSection.ContinueWatching));
    }

    [Fact]
    public void GetHidden_ReturnsACopy()
    {
        var item = Guid.NewGuid();
        _store.Hide(_user, item);

        _store.GetHidden(_user, HomeSection.RecentlyAdded).Clear();

        Assert.True(_store.IsHidden(_user, item));
    }

    [Fact]
    public void GetAllHidden_IgnoresSectionSettingsAndOtherUsers()
    {
        var item = Guid.NewGuid();
        _store.Hide(_user, item);
        _store.Hide(_otherUser, Guid.NewGuid());
        _store.SaveSettings(_user, new SectionSettings(false, false, false));

        Assert.Equal([item], _store.GetAllHidden(_user));
        Assert.Empty(_store.GetAllHidden(Guid.NewGuid()));
    }

    [Fact]
    public void GetAllHidden_ReturnsACopy()
    {
        var item = Guid.NewGuid();
        _store.Hide(_user, item);

        _store.GetAllHidden(_user).Clear();

        Assert.True(_store.IsHidden(_user, item));
    }

    [Fact]
    public async Task ConcurrentHides_AreAllKept()
    {
        var items = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(items.Select(item => Task.Run(() => _store.Hide(_user, item))));

        Assert.Equal(items.ToHashSet(), _store.GetHidden(_user, HomeSection.RecentlyAdded));
        Assert.Single(_harness.Plugin.Configuration.HiddenItems);
    }

    [Fact]
    public void UsingTheStoreBeforeThePluginIsLoaded_Throws()
    {
        PluginHarness.ClearInstance();

        Assert.Throws<InvalidOperationException>(() => _store.Hide(_user, Guid.NewGuid()));
    }

    [Fact]
    public void ResolveTarget_MovieAndSeriesResolveToThemselves()
    {
        var movie = Guid.NewGuid();
        var series = Guid.NewGuid();
        _libraryManager.GetItemById(movie).Returns(new Movie { Id = movie });
        _libraryManager.GetItemById(series).Returns(new Series { Id = series });

        Assert.Equal(movie, _store.ResolveTarget(movie));
        Assert.Equal(series, _store.ResolveTarget(series));
    }

    [Fact]
    public void ResolveTarget_EpisodeAndSeasonResolveToTheirSeries()
    {
        var series = Guid.NewGuid();
        var episode = Guid.NewGuid();
        var season = Guid.NewGuid();
        _libraryManager.GetItemById(episode).Returns(new Episode { Id = episode, SeriesId = series });
        _libraryManager.GetItemById(season).Returns(new Season { Id = season, SeriesId = series });

        Assert.Equal(series, _store.ResolveTarget(episode));
        Assert.Equal(series, _store.ResolveTarget(season));
    }

    [Fact]
    public void ResolveTarget_EpisodeOrSeasonWithoutSeries_IsNotEligible()
    {
        var episode = Guid.NewGuid();
        var season = Guid.NewGuid();
        _libraryManager.GetItemById(episode).Returns(new Episode { Id = episode });
        _libraryManager.GetItemById(season).Returns(new Season { Id = season });

        Assert.Null(_store.ResolveTarget(episode));
        Assert.Null(_store.ResolveTarget(season));
    }

    [Fact]
    public void ResolveTarget_OtherItemTypesAndUnknownIds_AreNotEligible()
    {
        var album = Guid.NewGuid();
        var folder = Guid.NewGuid();
        _libraryManager.GetItemById(album).Returns(new MusicAlbum { Id = album });
        _libraryManager.GetItemById(folder).Returns(new Folder { Id = folder });

        Assert.Null(_store.ResolveTarget(album));
        Assert.Null(_store.ResolveTarget(folder));
        Assert.Null(_store.ResolveTarget(Guid.NewGuid()));
    }
}

public class SectionSettingsTests
{
    [Theory]
    [InlineData(HomeSection.RecentlyAdded, true, false, false)]
    [InlineData(HomeSection.ContinueWatching, false, true, false)]
    [InlineData(HomeSection.NextUp, false, false, true)]
    public void AppliesTo_UsesTheMatchingFlag(HomeSection section, bool recentlyAdded, bool continueWatching, bool nextUp)
    {
        var settings = new SectionSettings(recentlyAdded, continueWatching, nextUp);
        var inverted = new SectionSettings(!recentlyAdded, !continueWatching, !nextUp);

        Assert.True(settings.AppliesTo(section));
        Assert.False(inverted.AppliesTo(section));
    }

    [Fact]
    public void AppliesTo_UnknownSection_IsFalse()
        => Assert.False(new SectionSettings(true, true, true).AppliesTo((HomeSection)99));
}

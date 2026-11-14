using Jellyfin.Plugin.LanguageFailover.Services;
using Xunit;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

public class StillPlayingTests
{
    private static readonly Guid Item = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
    private const string Source = "6f9619ff8b86d011b42d00cf4fc964ff";

    [Fact]
    public void SameItemAndSource()
    {
        Assert.True(PlaybackStartHandler.IsStillPlaying(Item, Source, Item, Source));
    }

    [Fact]
    public void AnotherItemStarted()
    {
        Assert.False(PlaybackStartHandler.IsStillPlaying(Guid.NewGuid(), Source, Item, Source));
    }

    [Fact]
    public void PlaybackStopped()
    {
        Assert.False(PlaybackStartHandler.IsStillPlaying(null, null, Item, Source));
    }

    [Fact]
    public void AnotherVersionOfTheSameItem()
    {
        Assert.False(PlaybackStartHandler.IsStillPlaying(Item, Guid.NewGuid().ToString("N"), Item, Source));
    }

    [Theory]
    [InlineData(null, Source)]
    [InlineData(Source, null)]
    [InlineData("", Source)]
    public void UnknownSourceFallsBackToTheItem(string? nowPlayingSource, string? startedSource)
    {
        Assert.True(PlaybackStartHandler.IsStillPlaying(Item, nowPlayingSource, Item, startedSource));
    }

    [Fact]
    public void SourceIdsInDifferentGuidFormatsMatch()
    {
        Assert.True(PlaybackStartHandler.IsStillPlaying(Item, Item.ToString("D"), Item, Source));
    }

    [Fact]
    public void NonGuidSourceIdsAreComparedAsText()
    {
        Assert.True(PlaybackStartHandler.IsStillPlaying(Item, "live_abc", Item, "LIVE_ABC"));
        Assert.False(PlaybackStartHandler.IsStillPlaying(Item, "live_abc", Item, "live_def"));
    }
}

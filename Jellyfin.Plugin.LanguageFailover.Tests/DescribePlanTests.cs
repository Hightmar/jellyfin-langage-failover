using Jellyfin.Plugin.LanguageFailover.Services;
using Xunit;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

public class DescribePlanTests
{
    [Fact]
    public void DescribesBothTracks()
    {
        var plan = new TrackSelectionPlan(2, "jpn", null, SubtitleAction.Select, 5);

        Assert.Equal(("#2 (jpn)", "#5"), PlaybackStartHandler.Describe(plan));
    }

    [Fact]
    public void NamesTheOriginalVersionSignal()
    {
        var plan = new TrackSelectionPlan(1, "ger", "track title", SubtitleAction.Disable, null);

        Assert.Equal(("#1 (ger, original via track title)", "off"), PlaybackStartHandler.Describe(plan));
    }

    [Fact]
    public void MarksForcedSubtitles()
    {
        var plan = new TrackSelectionPlan(null, "fre", null, SubtitleAction.SelectForced, 4);

        Assert.Equal(((string?)null, "#4 (forced)"), PlaybackStartHandler.Describe(plan));
    }

    [Theory]
    [InlineData(SubtitleAction.None)]
    [InlineData(SubtitleAction.NoMatch)]
    public void ReportsNothingWhenNothingChanges(SubtitleAction subtitle)
    {
        var plan = new TrackSelectionPlan(null, "fre", null, subtitle, null);

        Assert.Equal(((string?)null, (string?)null), PlaybackStartHandler.Describe(plan));
    }
}

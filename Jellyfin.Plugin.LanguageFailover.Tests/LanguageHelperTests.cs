using Jellyfin.Plugin.LanguageFailover.Services;
using MediaBrowser.Model.Entities;
using Xunit;
using static Jellyfin.Plugin.LanguageFailover.Tests.MediaStreamBuilder;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

public class IsForcedSubtitleTests
{
    [Fact]
    public void ContainerForcedFlagWins()
    {
        Assert.True(LanguageHelper.IsForcedSubtitle(Subtitle(0, "fr", isForced: true)));
    }

    [Theory]
    [InlineData("Forced")]
    [InlineData("French (Forced)")]
    [InlineData("Forcé")]
    [InlineData("Forcée")]
    [InlineData("Sous-titres forcés")]
    [InlineData("Forzado")]
    [InlineData("Forzados")]
    [InlineData("Forzato")]
    [InlineData("Forzati")]
    [InlineData("Erzwungen")]
    [InlineData("FORCED SDH")]
    public void ForcedKeywordInTitleIsDetected(string title)
    {
        Assert.True(LanguageHelper.IsForcedSubtitle(Subtitle(0, "fr", title: title)));
    }

    [Theory]
    [InlineData("Non-Forced")]
    [InlineData("non forcé")]
    [InlineData("Not forced")]
    [InlineData("Full / not forced")]
    [InlineData("NonForced")]
    public void NegatedForcedKeywordIsNotForced(string title)
    {
        Assert.False(LanguageHelper.IsForcedSubtitle(Subtitle(0, "fr", title: title)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Complete")]
    [InlineData("SDH")]
    [InlineData("Reinforced steel")]
    public void PlainTitleIsNotForced(string? title)
    {
        Assert.False(LanguageHelper.IsForcedSubtitle(Subtitle(0, "fr", title: title)));
    }

    [Fact]
    public void NegationInTitleDoesNotOverrideContainerFlag()
    {
        // The container flag is authoritative: a "non-forced" title cannot un-force it.
        Assert.True(LanguageHelper.IsForcedSubtitle(Subtitle(0, "fr", isForced: true, title: "Non-Forced")));
    }
}

public class LanguageMatchesTests
{
    private readonly FakeLocalizationManager _loc = new();

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("FR", "fr")]
    [InlineData("fre", "fre")]
    public void IdenticalCodesMatch(string stream, string preferred)
    {
        Assert.True(LanguageHelper.LanguageMatches(stream, preferred, _loc));
    }

    [Theory]
    [InlineData("fre", "fr")]   // ISO 639-2/B stream, ISO 639-1 preference
    [InlineData("fra", "fr")]   // ISO 639-2/T stream
    [InlineData("fr", "fre")]   // reverse direction
    [InlineData("fr", "fra")]
    [InlineData("ger", "de")]
    [InlineData("deu", "de")]
    [InlineData("de", "ger")]
    [InlineData("eng", "en")]
    [InlineData("EN", "eng")]
    public void CrossFormatCodesMatch(string stream, string preferred)
    {
        Assert.True(LanguageHelper.LanguageMatches(stream, preferred, _loc));
    }

    [Theory]
    [InlineData("fr", "en")]
    [InlineData("fre", "eng")]
    [InlineData("ja", "zh")]
    [InlineData(null, "fr")]
    [InlineData("", "fr")]
    [InlineData("fr", "")]
    [InlineData("qqq", "fr")]   // unknown code, no culture info
    public void MismatchedCodesDoNotMatch(string? stream, string preferred)
    {
        Assert.False(LanguageHelper.LanguageMatches(stream, preferred, _loc));
    }

    [Fact]
    public void UnknownButIdenticalCodesStillMatch()
    {
        // No culture info for "und", but a direct string match is enough.
        Assert.True(LanguageHelper.LanguageMatches("und", "und", _loc));
    }
}

public class SelectBestAudioStreamTests
{
    private readonly FakeLocalizationManager _loc = new();

    [Fact]
    public void PicksHighestPriorityLanguage()
    {
        var streams = new[] { Video(0), Audio(1, "en"), Audio(2, "fr"), Audio(3, "ja") };
        Assert.Equal(3, LanguageHelper.SelectBestAudioStream(streams, new[] { "ja", "fr", "en" }, _loc));
    }

    [Fact]
    public void FallsBackDownThePriorityList()
    {
        var streams = new[] { Video(0), Audio(1, "en"), Audio(2, "fr") };
        Assert.Equal(2, LanguageHelper.SelectBestAudioStream(streams, new[] { "ja", "fr", "en" }, _loc));
    }

    [Fact]
    public void PrefersHighestChannelCountWithinTheSameLanguage()
    {
        var streams = new[] { Audio(1, "fr", channels: 2), Audio(2, "fr", channels: 8), Audio(3, "fr", channels: 6) };
        Assert.Equal(2, LanguageHelper.SelectBestAudioStream(streams, new[] { "fr" }, _loc));
    }

    [Fact]
    public void TreatsMissingChannelCountAsZero()
    {
        var streams = new[] { Audio(1, "fr"), Audio(2, "fr", channels: 2) };
        Assert.Equal(2, LanguageHelper.SelectBestAudioStream(streams, new[] { "fr" }, _loc));
    }

    [Fact]
    public void ChannelCountNeverBeatsLanguagePriority()
    {
        var streams = new[] { Audio(1, "en", channels: 8), Audio(2, "ja", channels: 2) };
        Assert.Equal(2, LanguageHelper.SelectBestAudioStream(streams, new[] { "ja", "en" }, _loc));
    }

    [Fact]
    public void MatchesAcrossIsoCodeFormats()
    {
        var streams = new[] { Audio(1, "eng"), Audio(2, "fra") };
        Assert.Equal(2, LanguageHelper.SelectBestAudioStream(streams, new[] { "fr" }, _loc));
    }

    [Fact]
    public void IgnoresNonAudioStreams()
    {
        var streams = new[] { Video(0), Subtitle(1, "fr") };
        Assert.Null(LanguageHelper.SelectBestAudioStream(streams, new[] { "fr" }, _loc));
    }

    [Fact]
    public void ReturnsNullWhenNothingMatches()
    {
        var streams = new[] { Audio(1, "en") };
        Assert.Null(LanguageHelper.SelectBestAudioStream(streams, new[] { "ja" }, _loc));
    }

    [Fact]
    public void ReturnsNullOnEmptyInputs()
    {
        Assert.Null(LanguageHelper.SelectBestAudioStream(Array.Empty<MediaStream>(), new[] { "fr" }, _loc));
        Assert.Null(LanguageHelper.SelectBestAudioStream(new[] { Audio(1, "fr") }, Array.Empty<string>(), _loc));
    }
}

public class SelectOriginalAudioStreamTests
{
    [Theory]
    [InlineData("Original")]
    [InlineData("Original Audio")]
    [InlineData("Original Language")]
    [InlineData("Original Version")]
    [InlineData("Version Originale")]
    [InlineData("VO")]
    [InlineData("V.O.")]
    [InlineData("AC3 Original 5.1")]
    public void DetectsOriginalVersionKeywords(string title)
    {
        var streams = new[] { Audio(1, "en"), Audio(2, "ja", title: title) };
        Assert.Equal(2, LanguageHelper.SelectOriginalAudioStream(streams));
    }

    [Theory]
    [InlineData("Voice")]
    [InlineData("Volume 1")]
    [InlineData("VOSTFR")]
    [InlineData("Commentary")]
    [InlineData(null)]
    public void DoesNotMatchUnrelatedTitles(string? title)
    {
        var streams = new[] { Audio(1, "en"), Audio(2, "ja", title: title) };
        Assert.Null(LanguageHelper.SelectOriginalAudioStream(streams));
    }

    [Fact]
    public void PrefersHighestChannelCountAmongOriginalTracks()
    {
        var streams = new[]
        {
            Audio(1, "ja", channels: 2, title: "Original"),
            Audio(2, "ja", channels: 6, title: "Version Originale"),
        };
        Assert.Equal(2, LanguageHelper.SelectOriginalAudioStream(streams));
    }

    [Fact]
    public void IgnoresSubtitleStreamsTaggedOriginal()
    {
        var streams = new[] { Subtitle(1, "ja", title: "Original") };
        Assert.Null(LanguageHelper.SelectOriginalAudioStream(streams));
    }
}

public class SelectBestSubtitleStreamTests
{
    private readonly FakeLocalizationManager _loc = new();

    [Fact]
    public void PrefersNonForcedWhenAsked()
    {
        var streams = new[] { Subtitle(1, "fr", isForced: true), Subtitle(2, "fr") };
        Assert.Equal(2, LanguageHelper.SelectBestSubtitleStream(streams, new[] { "fr" }, true, _loc));
    }

    [Fact]
    public void PrefersNonForcedDetectedByTitle()
    {
        var streams = new[] { Subtitle(1, "fr", title: "Forcé"), Subtitle(2, "fr", title: "Complet") };
        Assert.Equal(2, LanguageHelper.SelectBestSubtitleStream(streams, new[] { "fr" }, true, _loc));
    }

    [Fact]
    public void FallsBackToForcedWhenNoCompleteTrackExists()
    {
        var streams = new[] { Subtitle(1, "fr", isForced: true) };
        Assert.Equal(1, LanguageHelper.SelectBestSubtitleStream(streams, new[] { "fr" }, true, _loc));
    }

    [Fact]
    public void HigherPriorityLanguageWinsOverForcedPreference()
    {
        var streams = new[] { Subtitle(1, "en", isForced: true), Subtitle(2, "fr") };
        Assert.Equal(1, LanguageHelper.SelectBestSubtitleStream(streams, new[] { "en", "fr" }, true, _loc));
    }

    [Fact]
    public void ReturnsNullWhenNothingMatches()
    {
        var streams = new[] { Subtitle(1, "en") };
        Assert.Null(LanguageHelper.SelectBestSubtitleStream(streams, new[] { "ja" }, true, _loc));
    }

    [Fact]
    public void IgnoresAudioStreams()
    {
        var streams = new[] { Audio(1, "fr") };
        Assert.Null(LanguageHelper.SelectBestSubtitleStream(streams, new[] { "fr" }, true, _loc));
    }
}

public class SelectForcedSubtitleForLanguageTests
{
    private readonly FakeLocalizationManager _loc = new();

    [Fact]
    public void FindsForcedTrackInTheRequestedLanguage()
    {
        var streams = new[] { Subtitle(1, "en", isForced: true), Subtitle(2, "fr", isForced: true), Subtitle(3, "fr") };
        Assert.Equal(2, LanguageHelper.SelectForcedSubtitleForLanguage(streams, "fr", _loc));
    }

    [Fact]
    public void FindsForcedTrackDetectedByTitle()
    {
        var streams = new[] { Subtitle(1, "fra", title: "Forcés") };
        Assert.Equal(1, LanguageHelper.SelectForcedSubtitleForLanguage(streams, "fr", _loc));
    }

    [Fact]
    public void ReturnsNullWhenOnlyCompleteTracksExist()
    {
        var streams = new[] { Subtitle(1, "fr"), Subtitle(2, "fr", title: "Non-Forced") };
        Assert.Null(LanguageHelper.SelectForcedSubtitleForLanguage(streams, "fr", _loc));
    }
}

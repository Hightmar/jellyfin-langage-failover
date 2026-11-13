using Jellyfin.Plugin.LanguageFailover.Configuration;
using Jellyfin.Plugin.LanguageFailover.Services;
using MediaBrowser.Model.Entities;
using Xunit;
using static Jellyfin.Plugin.LanguageFailover.Tests.MediaStreamBuilder;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

public class TrackSelectionPlannerTests
{
    private readonly FakeLocalizationManager _loc = new();

    private static UserLanguagePreference Prefs(
        string[]? audio = null,
        string[]? subtitles = null,
        bool preferOriginal = false,
        bool forcedWhenAudioMatches = true) => new()
    {
        AudioLanguages = (audio ?? Array.Empty<string>()).ToList(),
        SubtitleLanguages = (subtitles ?? Array.Empty<string>()).ToList(),
        PreferOriginalAudio = preferOriginal,
        PreferForcedWhenAudioMatches = forcedWhenAudioMatches,
    };

    private TrackSelectionPlan Plan(MediaStream[] streams, UserLanguagePreference prefs, string? originalLanguage = null)
        => TrackSelectionPlanner.Plan(streams, prefs, originalLanguage, _loc);

    [Fact]
    public void OriginalAudioWinsOverThePriorityList()
    {
        var plan = Plan(
            new[] { Audio(1, "en"), Audio(2, "ja", title: "VO") },
            Prefs(audio: new[] { "en" }, preferOriginal: true));

        Assert.Equal(2, plan.AudioIndex);
        Assert.Equal("ja", plan.AudioLanguage);
        Assert.Equal("track title", plan.OriginalSignal);
    }

    [Fact]
    public void FallsBackToThePriorityListWhenNoTrackIsOriginal()
    {
        var plan = Plan(
            new[] { Audio(1, "en"), Audio(2, "ja") },
            Prefs(audio: new[] { "ja" }, preferOriginal: true));

        Assert.Equal(2, plan.AudioIndex);
        Assert.Null(plan.OriginalSignal);
    }

    [Fact]
    public void OriginalLanguageFromMetadataIsUsedWhenAsked()
    {
        var plan = Plan(
            new[] { Audio(1, "zh"), Audio(2, "de") },
            Prefs(audio: new[] { "zh" }, preferOriginal: true),
            originalLanguage: "de");

        Assert.Equal(2, plan.AudioIndex);
    }

    [Fact]
    public void LeavesAudioAloneAndReportsTheDefaultTrackWhenNothingMatches()
    {
        var plan = Plan(
            new[] { Audio(1, "fr"), Audio(2, "de") },
            Prefs(audio: new[] { "ja" }));

        Assert.Null(plan.AudioIndex);
        Assert.Equal("fr", plan.AudioLanguage);
    }

    [Fact]
    public void LeavesSubtitlesAloneWithoutSubtitlePreferences()
    {
        var plan = Plan(
            new[] { Audio(1, "ja"), Subtitle(2, "en") },
            Prefs(audio: new[] { "ja" }));

        Assert.Equal(SubtitleAction.None, plan.Subtitle);
        Assert.Null(plan.SubtitleIndex);
    }

    [Fact]
    public void SelectsTheBestSubtitleWhenTheAudioIsNotReadable()
    {
        var plan = Plan(
            new[] { Audio(1, "ja"), Subtitle(2, "fr"), Subtitle(3, "en") },
            Prefs(audio: new[] { "ja" }, subtitles: new[] { "en", "fr" }));

        Assert.Equal(SubtitleAction.Select, plan.Subtitle);
        Assert.Equal(3, plan.SubtitleIndex);
    }

    [Fact]
    public void SelectsAForcedTrackWhenTheAudioIsAlreadyReadable()
    {
        var plan = Plan(
            new[] { Audio(1, "fr"), Subtitle(2, "fr"), Subtitle(3, "fr", isForced: true) },
            Prefs(audio: new[] { "fr" }, subtitles: new[] { "fr" }));

        Assert.Equal(SubtitleAction.SelectForced, plan.Subtitle);
        Assert.Equal(3, plan.SubtitleIndex);
    }

    [Fact]
    public void DisablesSubtitlesWhenTheAudioIsReadableAndNothingIsForced()
    {
        var plan = Plan(
            new[] { Audio(1, "fr"), Subtitle(2, "fr") },
            Prefs(audio: new[] { "fr" }, subtitles: new[] { "fr" }));

        Assert.Equal(SubtitleAction.Disable, plan.Subtitle);
        Assert.Null(plan.SubtitleIndex);
    }

    [Fact]
    public void DisablesSubtitlesWhenForcedTracksAreNotWanted()
    {
        var plan = Plan(
            new[] { Audio(1, "fr"), Subtitle(2, "fr", isForced: true) },
            Prefs(audio: new[] { "fr" }, subtitles: new[] { "fr" }, forcedWhenAudioMatches: false));

        Assert.Equal(SubtitleAction.Disable, plan.Subtitle);
    }

    [Fact]
    public void UsesTheDefaultAudioTrackForTheSubtitleDecision()
    {
        var plan = Plan(
            new[] { Audio(1, "fr"), Subtitle(2, "fr") },
            Prefs(subtitles: new[] { "fr" }));

        Assert.Null(plan.AudioIndex);
        Assert.Equal(SubtitleAction.Disable, plan.Subtitle);
    }

    [Fact]
    public void ReportsWhenNoSubtitleMatches()
    {
        var plan = Plan(
            new[] { Audio(1, "ja"), Subtitle(2, "de") },
            Prefs(audio: new[] { "ja" }, subtitles: new[] { "en" }));

        Assert.Equal(SubtitleAction.NoMatch, plan.Subtitle);
        Assert.Null(plan.SubtitleIndex);
    }
}

public class SeriesOverrideTests
{
    private static readonly Guid SeriesId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    private static UserLanguagePreference PrefsWith(SeriesLanguageOverride seriesOverride) => new()
    {
        AudioLanguages = new List<string> { "en" },
        SubtitleLanguages = new List<string> { "fr" },
        PreferOriginalAudio = true,
        PreferNonForcedSubtitles = false,
        PreferForcedWhenAudioMatches = false,
        SeriesOverrides = new List<SeriesLanguageOverride> { seriesOverride },
    };

    [Fact]
    public void FindsTheOverrideWhateverTheCaseOfTheStoredId()
    {
        var prefs = PrefsWith(new SeriesLanguageOverride { SeriesId = SeriesId.ToString("N").ToUpperInvariant() });

        Assert.NotNull(TrackSelectionPlanner.FindSeriesOverride(prefs, SeriesId));
    }

    [Fact]
    public void FindsNothingForAnotherSeries()
    {
        var prefs = PrefsWith(new SeriesLanguageOverride { SeriesId = SeriesId.ToString("N") });

        Assert.Null(TrackSelectionPlanner.FindSeriesOverride(prefs, Guid.NewGuid()));
    }

    [Fact]
    public void AnEmptyOverrideListInheritsTheGlobalOne()
    {
        var seriesOverride = new SeriesLanguageOverride
        {
            SeriesId = SeriesId.ToString("N"),
            AudioLanguages = new List<string> { "ja" },
        };

        var effective = TrackSelectionPlanner.ApplySeriesOverride(PrefsWith(seriesOverride), seriesOverride);

        Assert.Equal(new[] { "ja" }, effective.AudioLanguages);
        Assert.Equal(new[] { "fr" }, effective.SubtitleLanguages);
    }

    [Fact]
    public void KeepsTheUsersToggles()
    {
        var seriesOverride = new SeriesLanguageOverride { SeriesId = SeriesId.ToString("N") };

        var effective = TrackSelectionPlanner.ApplySeriesOverride(PrefsWith(seriesOverride), seriesOverride);

        Assert.True(effective.PreferOriginalAudio);
        Assert.False(effective.PreferNonForcedSubtitles);
        Assert.False(effective.PreferForcedWhenAudioMatches);
    }

    [Fact]
    public void WithoutAnOverrideTheGlobalListsApply()
    {
        var prefs = PrefsWith(new SeriesLanguageOverride { SeriesId = SeriesId.ToString("N") });

        var effective = TrackSelectionPlanner.ApplySeriesOverride(prefs, null);

        Assert.Equal(new[] { "en" }, effective.AudioLanguages);
        Assert.Equal(new[] { "fr" }, effective.SubtitleLanguages);
    }
}

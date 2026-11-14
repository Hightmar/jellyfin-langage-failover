using Jellyfin.Plugin.LanguageFailover.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.LanguageFailover.Services;

/// <summary>
/// What to do with the subtitle track.
/// </summary>
public enum SubtitleAction
{
    /// <summary>No subtitle preferences: leave the track alone.</summary>
    None,

    /// <summary>No subtitle stream matches the preferences: leave the track alone.</summary>
    NoMatch,

    /// <summary>Select the best matching subtitle stream.</summary>
    Select,

    /// <summary>The audio is already readable: select a forced stream in its language.</summary>
    SelectForced,

    /// <summary>The audio is already readable: turn subtitles off.</summary>
    Disable,
}

/// <summary>
/// The tracks to select for one playback.
/// </summary>
/// <param name="AudioIndex">The audio stream to select, or null to leave the client's choice.</param>
/// <param name="AudioLanguage">The language of the audio that will play.</param>
/// <param name="OriginalSignal">How the original version was recognised, when it chose the audio.</param>
/// <param name="Subtitle">What to do with subtitles.</param>
/// <param name="SubtitleIndex">The subtitle stream to select, for <see cref="SubtitleAction.Select"/> and <see cref="SubtitleAction.SelectForced"/>.</param>
public sealed record TrackSelectionPlan(
    int? AudioIndex,
    string? AudioLanguage,
    string? OriginalSignal,
    SubtitleAction Subtitle,
    int? SubtitleIndex);

/// <summary>
/// Decides which tracks to select. Pure: no I/O, no timing.
/// </summary>
public static class TrackSelectionPlanner
{
    /// <summary>
    /// Finds the user's override for a series, if any.
    /// </summary>
    /// <param name="prefs">The user's preferences.</param>
    /// <param name="seriesId">The series id.</param>
    /// <returns>The override, or null.</returns>
    public static SeriesLanguageOverride? FindSeriesOverride(UserLanguagePreference prefs, Guid seriesId)
    {
        var key = seriesId.ToString("N");
        return prefs.SeriesOverrides.Find(o => string.Equals(o.SeriesId, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the preferences to apply, with the override's non-empty lists replacing the global ones.
    /// </summary>
    /// <param name="prefs">The user's preferences.</param>
    /// <param name="seriesOverride">The series override, or null.</param>
    /// <returns>A new preference object; <paramref name="prefs"/> is not modified.</returns>
    public static UserLanguagePreference ApplySeriesOverride(UserLanguagePreference prefs, SeriesLanguageOverride? seriesOverride)
    {
        var audio = seriesOverride?.AudioLanguages.Count > 0 ? seriesOverride.AudioLanguages : prefs.AudioLanguages;
        var subtitles = seriesOverride?.SubtitleLanguages.Count > 0 ? seriesOverride.SubtitleLanguages : prefs.SubtitleLanguages;

        return new UserLanguagePreference
        {
            UserId = prefs.UserId,
            Enabled = prefs.Enabled,
            AudioLanguages = audio.ToList(),
            SubtitleLanguages = subtitles.ToList(),
            PreferNonForcedSubtitles = prefs.PreferNonForcedSubtitles,
            PreferOriginalAudio = prefs.PreferOriginalAudio,
            PreferForcedWhenAudioMatches = prefs.PreferForcedWhenAudioMatches,
        };
    }

    /// <summary>
    /// Plans the audio and subtitle selection for one item.
    /// </summary>
    /// <param name="streams">All media streams of the played source.</param>
    /// <param name="prefs">The effective preferences.</param>
    /// <param name="originalLanguage">The item's original language from its metadata, or null.</param>
    /// <param name="localizationManager">The localization manager, for ISO 639 matching.</param>
    /// <returns>The plan.</returns>
    public static TrackSelectionPlan Plan(
        IReadOnlyList<MediaStream> streams,
        UserLanguagePreference prefs,
        string? originalLanguage,
        ILocalizationManager localizationManager)
    {
        int? audioIndex = null;
        string? originalSignal = null;

        if (prefs.PreferOriginalAudio)
        {
            var original = LanguageHelper.SelectOriginalAudioStream(streams, originalLanguage, localizationManager);
            if (original is not null)
            {
                audioIndex = original.Value.Index;
                originalSignal = original.Value.Signal;
            }
        }

        if (audioIndex is null && prefs.AudioLanguages.Count > 0)
        {
            audioIndex = LanguageHelper.SelectBestAudioStream(streams, prefs.AudioLanguages, localizationManager);
        }

        // With no audio command the client plays its default track, so decide subtitles against that.
        var audioLanguage = audioIndex is null
            ? LanguageHelper.GetDefaultAudioLanguage(streams)
            : streams.FirstOrDefault(s => s.Index == audioIndex.Value)?.Language;

        var (subtitle, subtitleIndex) = PlanSubtitles(streams, prefs, audioLanguage, localizationManager);

        return new TrackSelectionPlan(audioIndex, audioLanguage, originalSignal, subtitle, subtitleIndex);
    }

    private static (SubtitleAction Action, int? Index) PlanSubtitles(
        IReadOnlyList<MediaStream> streams,
        UserLanguagePreference prefs,
        string? audioLanguage,
        ILocalizationManager localizationManager)
    {
        if (prefs.SubtitleLanguages.Count == 0)
        {
            return (SubtitleAction.None, null);
        }

        if (LanguageHelper.AudioMakesSubtitlesRedundant(streams, prefs.SubtitleLanguages, audioLanguage, localizationManager))
        {
            if (prefs.PreferForcedWhenAudioMatches)
            {
                var forced = LanguageHelper.SelectForcedSubtitleForLanguage(streams, audioLanguage!, localizationManager);
                if (forced is not null)
                {
                    return (SubtitleAction.SelectForced, forced);
                }
            }

            return (SubtitleAction.Disable, null);
        }

        var best = LanguageHelper.SelectBestSubtitleStream(
            streams,
            prefs.SubtitleLanguages,
            prefs.PreferNonForcedSubtitles,
            localizationManager);

        return best is null ? (SubtitleAction.NoMatch, null) : (SubtitleAction.Select, best);
    }
}

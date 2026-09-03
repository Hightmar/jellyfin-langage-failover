using System.Text.RegularExpressions;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.LanguageFailover.Services;

/// <summary>
/// Helper for language matching and stream selection.
/// </summary>
public static class LanguageHelper
{
    private static readonly Regex OriginalVersionRegex = new(
        @"\b(original|original\s+audio|original\s+language|original\s+version|version\s+originale|v\.?\s*o\.?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForcedSubtitleRegex = new(
        @"\b(forced|forc[ée]e?s?|forzado(?:s)?|forzat[io]|erzwungen)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Guards against false positives like "non-forced", "non forcé", "not forced".
    private static readonly Regex NonForcedSubtitleRegex = new(
        @"\b(?:non|not)[\s-]?forc",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Determines whether a subtitle stream is "forced".
    /// Many media files do not set the container forced disposition flag and instead
    /// indicate it only in the stream title (e.g. "Forced", "Forcé"), so this checks both
    /// the <see cref="MediaStream.IsForced"/> flag and forced keywords in the stream title
    /// and the composed display title.
    /// </summary>
    /// <param name="stream">The subtitle stream to inspect.</param>
    /// <returns>True if the stream is forced.</returns>
    public static bool IsForcedSubtitle(MediaStream stream)
    {
        if (stream.IsForced)
        {
            return true;
        }

        return TitleIndicatesForced(stream.Title) || TitleIndicatesForced(stream.DisplayTitle);
    }

    private static bool TitleIndicatesForced(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return false;
        }

        // A title that explicitly negates "forced" (e.g. "Non-Forced", "Full / not forced")
        // describes a complete track, so do not treat it as forced.
        if (NonForcedSubtitleRegex.IsMatch(title))
        {
            return false;
        }

        return ForcedSubtitleRegex.IsMatch(title);
    }

    /// <summary>
    /// Checks if a stream's language matches a preferred language code,
    /// handling ISO 639-1 (2-letter) and ISO 639-2 (3-letter) cross-matching.
    /// </summary>
    /// <param name="streamLanguage">The language code from the media stream.</param>
    /// <param name="preferredLanguage">The user's preferred language code.</param>
    /// <param name="localizationManager">The localization manager for language info lookup.</param>
    /// <returns>True if the languages match.</returns>
    public static bool LanguageMatches(string? streamLanguage, string preferredLanguage, ILocalizationManager localizationManager)
    {
        if (string.IsNullOrEmpty(streamLanguage) || string.IsNullOrEmpty(preferredLanguage))
        {
            return false;
        }

        // Direct match (case-insensitive)
        if (streamLanguage.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Cross-format match: resolve each side's culture and look for the other side's
        // code among its known ISO names. Both directions are tried because media may be
        // tagged in either format and the preference list is stored in either format.
        return CultureKnowsCode(localizationManager.FindLanguageInfo(streamLanguage), preferredLanguage)
               || CultureKnowsCode(localizationManager.FindLanguageInfo(preferredLanguage), streamLanguage);
    }

    /// <summary>
    /// Determines whether a resolved culture is known under the given ISO code,
    /// in either the 2-letter or any of its 3-letter forms.
    /// </summary>
    private static bool CultureKnowsCode(CultureDto? culture, string code)
    {
        if (culture is null)
        {
            return false;
        }

        if (code.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)
            || code.Equals(culture.ThreeLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (culture.ThreeLetterISOLanguageNames is null)
        {
            return false;
        }

        foreach (var known in culture.ThreeLetterISOLanguageNames)
        {
            if (code.Equals(known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Selects the audio stream marked as the original version (via stream title keywords
    /// like "Original", "VO", "Version Originale"). Among matching streams, prefers the
    /// highest channel count.
    /// </summary>
    /// <param name="streams">All media streams for the item.</param>
    /// <returns>The index of the best original audio stream, or null if none is tagged as such.</returns>
    public static int? SelectOriginalAudioStream(IReadOnlyList<MediaStream> streams)
    {
        var candidates = streams
            .Where(s => s.Type == MediaStreamType.Audio)
            .Where(s => !string.IsNullOrEmpty(s.Title) && OriginalVersionRegex.IsMatch(s.Title))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates.OrderByDescending(s => s.Channels ?? 0).First().Index;
    }

    /// <summary>
    /// Selects a forced subtitle stream in the given language, if available.
    /// </summary>
    /// <param name="streams">All media streams for the item.</param>
    /// <param name="language">The language the subtitle must be in.</param>
    /// <param name="localizationManager">The localization manager.</param>
    /// <returns>The index of the first forced subtitle matching the language, or null.</returns>
    public static int? SelectForcedSubtitleForLanguage(
        IReadOnlyList<MediaStream> streams,
        string language,
        ILocalizationManager localizationManager)
    {
        var forced = streams
            .Where(s => s.Type == MediaStreamType.Subtitle && IsForcedSubtitle(s))
            .FirstOrDefault(s => LanguageMatches(s.Language, language, localizationManager));

        return forced?.Index;
    }

    /// <summary>
    /// Selects the best audio stream index based on the user's language priority list.
    /// Among streams matching the same language, prefers higher channel count (surround over stereo).
    /// </summary>
    /// <param name="streams">All media streams for the item.</param>
    /// <param name="preferredLanguages">Ordered language codes (index 0 = highest priority).</param>
    /// <param name="localizationManager">The localization manager.</param>
    /// <returns>The best audio stream index, or null if no match found.</returns>
    public static int? SelectBestAudioStream(
        IReadOnlyList<MediaStream> streams,
        IList<string> preferredLanguages,
        ILocalizationManager localizationManager)
        => SelectByLanguagePriority(
            streams,
            MediaStreamType.Audio,
            preferredLanguages,
            localizationManager,
            // Prefer highest channel count (e.g., 7.1 > 5.1 > stereo)
            matches => matches.OrderByDescending(s => s.Channels ?? 0).First());

    /// <summary>
    /// Selects the best subtitle stream index based on the user's language priority list.
    /// When preferNonForced is true, prefers non-forced (complete) subtitles over forced ones.
    /// </summary>
    /// <param name="streams">All media streams for the item.</param>
    /// <param name="preferredLanguages">Ordered language codes (index 0 = highest priority).</param>
    /// <param name="preferNonForced">If true, prefer non-forced subtitles when available.</param>
    /// <param name="localizationManager">The localization manager.</param>
    /// <returns>The best subtitle stream index, or null if no match found.</returns>
    public static int? SelectBestSubtitleStream(
        IReadOnlyList<MediaStream> streams,
        IList<string> preferredLanguages,
        bool preferNonForced,
        ILocalizationManager localizationManager)
        => SelectByLanguagePriority(
            streams,
            MediaStreamType.Subtitle,
            preferredLanguages,
            localizationManager,
            matches => preferNonForced
                // Fall back to any match (including forced) if no non-forced is available.
                ? matches.FirstOrDefault(s => !IsForcedSubtitle(s)) ?? matches[0]
                : matches[0]);

    /// <summary>
    /// Walks the preference list in order and returns the index of the stream chosen by
    /// <paramref name="pickAmongMatches"/> from the first language that has any match.
    /// Language priority always wins: a tie-break never promotes a lower-priority language.
    /// </summary>
    /// <param name="streams">All media streams for the item.</param>
    /// <param name="type">The stream type to consider.</param>
    /// <param name="preferredLanguages">Ordered language codes (index 0 = highest priority).</param>
    /// <param name="localizationManager">The localization manager.</param>
    /// <param name="pickAmongMatches">Tie-break applied to the non-empty matches of one language.</param>
    /// <returns>The selected stream index, or null if no language matched.</returns>
    private static int? SelectByLanguagePriority(
        IReadOnlyList<MediaStream> streams,
        MediaStreamType type,
        IList<string> preferredLanguages,
        ILocalizationManager localizationManager,
        Func<List<MediaStream>, MediaStream> pickAmongMatches)
    {
        var candidates = streams.Where(s => s.Type == type).ToList();
        if (candidates.Count == 0 || preferredLanguages.Count == 0)
        {
            return null;
        }

        foreach (var lang in preferredLanguages)
        {
            var matches = candidates
                .Where(s => LanguageMatches(s.Language, lang, localizationManager))
                .ToList();

            if (matches.Count > 0)
            {
                return pickAmongMatches(matches).Index;
            }
        }

        return null;
    }
}

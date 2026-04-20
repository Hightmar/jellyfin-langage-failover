using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.LanguageFailover.Services;

/// <summary>
/// Helper for language matching and stream selection.
/// </summary>
public static class LanguageHelper
{
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

        // Cross-format match via localization manager
        var streamCulture = localizationManager.FindLanguageInfo(streamLanguage);
        if (streamCulture is not null)
        {
            if (preferredLanguage.Equals(streamCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)
                || preferredLanguage.Equals(streamCulture.ThreeLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (streamCulture.ThreeLetterISOLanguageNames is not null)
            {
                foreach (var code in streamCulture.ThreeLetterISOLanguageNames)
                {
                    if (preferredLanguage.Equals(code, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        // Reverse: resolve the preferred language and compare against stream language
        var prefCulture = localizationManager.FindLanguageInfo(preferredLanguage);
        if (prefCulture is not null)
        {
            if (streamLanguage.Equals(prefCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)
                || streamLanguage.Equals(prefCulture.ThreeLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (prefCulture.ThreeLetterISOLanguageNames is not null)
            {
                foreach (var code in prefCulture.ThreeLetterISOLanguageNames)
                {
                    if (streamLanguage.Equals(code, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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
    {
        var audioStreams = streams.Where(s => s.Type == MediaStreamType.Audio).ToList();
        if (audioStreams.Count == 0 || preferredLanguages.Count == 0)
        {
            return null;
        }

        foreach (var lang in preferredLanguages)
        {
            var matches = audioStreams
                .Where(s => LanguageMatches(s.Language, lang, localizationManager))
                .ToList();

            if (matches.Count > 0)
            {
                // Prefer highest channel count (e.g., 7.1 > 5.1 > stereo)
                return matches
                    .OrderByDescending(s => s.Channels ?? 0)
                    .First()
                    .Index;
            }
        }

        return null;
    }

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
    {
        var subtitleStreams = streams.Where(s => s.Type == MediaStreamType.Subtitle).ToList();
        if (subtitleStreams.Count == 0 || preferredLanguages.Count == 0)
        {
            return null;
        }

        foreach (var lang in preferredLanguages)
        {
            var matches = subtitleStreams
                .Where(s => LanguageMatches(s.Language, lang, localizationManager))
                .ToList();

            if (matches.Count > 0)
            {
                if (preferNonForced)
                {
                    var nonForced = matches.Where(s => !s.IsForced).ToList();
                    if (nonForced.Count > 0)
                    {
                        return nonForced.First().Index;
                    }
                }

                // Fall back to any match (including forced) if no non-forced available
                return matches.First().Index;
            }
        }

        return null;
    }
}

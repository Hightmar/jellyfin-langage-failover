namespace Jellyfin.Plugin.LanguageFailover.Configuration;

/// <summary>
/// Per-user language preference with ordered fallback lists.
/// </summary>
public class UserLanguagePreference
{
    /// <summary>
    /// Gets or sets the user ID (Guid without hyphens).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered list of preferred audio language codes (ISO 639-1/2).
    /// Index 0 is the highest priority.
    /// </summary>
    public List<string> AudioLanguages { get; set; } = new();

    /// <summary>
    /// Gets or sets the ordered list of preferred subtitle language codes (ISO 639-1/2).
    /// Index 0 is the highest priority.
    /// </summary>
    public List<string> SubtitleLanguages { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to prefer non-forced (complete) subtitles
    /// over forced subtitles when both are available for a matching language.
    /// </summary>
    public bool PreferNonForcedSubtitles { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether language failover is enabled for this user.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to prefer the original-version audio track
    /// (detected via stream title keywords like "original", "VO", "version originale")
    /// over the priority list.
    /// </summary>
    public bool PreferOriginalAudio { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show forced subtitles when the audio is
    /// already in one of the user's subtitle languages. Useful for translating foreign
    /// dialog in an otherwise native-language film.
    /// </summary>
    public bool PreferForcedWhenAudioMatches { get; set; } = true;

    /// <summary>
    /// Gets or sets per-series language overrides.
    /// When a series has an override, its languages take priority over the global user preferences.
    /// </summary>
    public List<SeriesLanguageOverride> SeriesOverrides { get; set; } = new();
}

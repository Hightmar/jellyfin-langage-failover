namespace Jellyfin.Plugin.LanguageFailover.Configuration;

/// <summary>
/// Language override for a specific series.
/// </summary>
public class SeriesLanguageOverride
{
    /// <summary>
    /// Gets or sets the series ID (Guid without hyphens).
    /// </summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the series name (for display in the config page).
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered list of preferred audio language codes.
    /// </summary>
    public List<string> AudioLanguages { get; set; } = new();

    /// <summary>
    /// Gets or sets the ordered list of preferred subtitle language codes.
    /// </summary>
    public List<string> SubtitleLanguages { get; set; } = new();
}

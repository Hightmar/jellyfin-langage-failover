using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LanguageFailover.Configuration;

/// <summary>
/// Plugin configuration containing per-user language preferences.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the per-user language preferences.
    /// </summary>
    public List<UserLanguagePreference> UserPreferences { get; set; } = new();
}

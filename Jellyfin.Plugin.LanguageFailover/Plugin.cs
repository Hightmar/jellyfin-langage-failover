using Jellyfin.Plugin.LanguageFailover.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.LanguageFailover;

/// <summary>
/// Language Failover plugin for Jellyfin.
/// Allows per-user audio and subtitle language priority lists with fallback.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Language Failover";

    /// <inheritdoc />
    public override string Description => "Per-user audio and subtitle language priority with fallback.";

    /// <inheritdoc />
    public override Guid Id => new Guid("a5b6c7d8-1234-5678-9abc-def012345678");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Pages.configPage.html"
            },
            new PluginPageInfo
            {
                Name = $"{Name}.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.Pages.configPage.js"
            }
        };
    }
}

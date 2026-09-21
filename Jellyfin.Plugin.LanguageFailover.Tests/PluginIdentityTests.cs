using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

/// <summary>
/// The plugin's GUID is restated in Plugin.cs, meta.json, manifest.json and the
/// admin page's JS, and there is no way to make C#, JSON and browser JS share one
/// declaration. These tests turn a divergence into a build failure instead of a
/// config page that silently writes to a plugin id the server does not know.
///
/// Plugin.cs is read as text rather than instantiated: BasePlugin's constructor
/// needs a live IApplicationPaths and IXmlSerializer.
/// </summary>
public class PluginIdentityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string PluginDir => Path.Combine(RepoRoot, "Jellyfin.Plugin.LanguageFailover");

    private static string MetaJsonPath => Path.Combine(PluginDir, "meta.json");

    /// <summary>
    /// The GUID as declared by the Plugin type — the one Jellyfin registers.
    /// </summary>
    private static string PluginId
    {
        get
        {
            var source = File.ReadAllText(Path.Combine(PluginDir, "Plugin.cs"));
            var match = Regex.Match(source, @"Id\s*=>\s*new\s+Guid\(""([0-9a-fA-F-]+)""\)");
            Assert.True(match.Success, "Plugin.cs no longer declares Id as a Guid literal.");
            return match.Groups[1].Value;
        }
    }

    [Fact]
    public void MetaJsonGuidMatchesPluginId()
    {
        using var meta = JsonDocument.Parse(File.ReadAllText(MetaJsonPath));
        Assert.Equal(PluginId, meta.RootElement.GetProperty("guid").GetString(), ignoreCase: true);
    }

    [Fact]
    public void ConfigPageGuidMatchesPluginId()
    {
        var js = File.ReadAllText(Path.Combine(PluginDir, "Pages", "configPage.js"));

        var match = Regex.Match(js, @"pluginId\s*=\s*'([0-9a-fA-F-]+)'");
        Assert.True(match.Success, "configPage.js no longer declares pluginId in the expected form.");
        Assert.Equal(PluginId, match.Groups[1].Value, ignoreCase: true);
    }

    [Fact]
    public void ManifestHasExactlyOneEntryAgreeingWithMetaJson()
    {
        using var meta = JsonDocument.Parse(File.ReadAllText(MetaJsonPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "manifest.json")));

        var guid = meta.RootElement.GetProperty("guid").GetString();
        var entries = manifest.RootElement.EnumerateArray()
            .Where(p => p.GetProperty("guid").GetString() == guid)
            .ToList();

        Assert.True(entries.Count == 1, $"manifest.json must contain exactly one entry for {guid}, found {entries.Count}.");
        Assert.Equal(meta.RootElement.GetProperty("name").GetString(), entries[0].GetProperty("name").GetString());
    }

    [Fact]
    public void PluginImageExistsAndManifestPointsAtIt()
    {
        // meta.json names the file the release zip ships beside the DLL ("My Plugins");
        // manifest.json points the catalogue at the same file on main. Both are read
        // from images/, so a rename that misses one of them fails here, not in the UI.
        using var meta = JsonDocument.Parse(File.ReadAllText(MetaJsonPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "manifest.json")));

        var imagePath = meta.RootElement.GetProperty("imagePath").GetString()!;
        Assert.True(
            File.Exists(Path.Combine(RepoRoot, "images", imagePath)),
            $"meta.json declares imagePath {imagePath}, but images/{imagePath} does not exist.");

        var guid = meta.RootElement.GetProperty("guid").GetString();
        var plugin = manifest.RootElement.EnumerateArray().First(p => p.GetProperty("guid").GetString() == guid);
        Assert.EndsWith($"/main/images/{imagePath}", plugin.GetProperty("imageUrl").GetString());
    }

    [Fact]
    public void MetaJsonCarriesFourSegmentVersions()
    {
        // Jellyfin rejects anything else; scripts/set_meta_version.py enforces the
        // same shape at release time.
        using var meta = JsonDocument.Parse(File.ReadAllText(MetaJsonPath));

        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", meta.RootElement.GetProperty("version").GetString());
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", meta.RootElement.GetProperty("targetAbi").GetString());
    }

    [Fact]
    public void ManifestTargetAbisStayBehindTheBuild()
    {
        // The manifest outlives a server generation: 1.2.0.1 keeps serving Jellyfin
        // 10.11 long after meta.json moved to the 12.x ABI. So entries may declare an
        // older ABI than meta.json — but never a newer one, which would mean the
        // catalogue is promising a build that does not exist yet.
        var metaAbi = Version.Parse(MetaTargetAbi);

        foreach (var (version, abi) in ManifestVersions())
        {
            Assert.True(
                abi <= metaAbi,
                $"manifest entry {version} declares targetAbi {abi}, ahead of meta.json's {metaAbi}.");
        }
    }

    [Fact]
    public void NewerVersionsNeverDeclareAnOlderAbi()
    {
        // Jellyfin filters the catalogue with `targetAbi <= serverVersion`, not equality,
        // so a 12.x server sees the 10.11 entries too and simply takes the highest version
        // number of everything it can see. The moment an old-ABI entry outranks a new-ABI
        // one, that server installs the build that cannot load on it.
        var entries = ManifestVersions();

        foreach (var older in entries)
        {
            foreach (var newer in entries)
            {
                if (older.Abi < newer.Abi)
                {
                    Assert.True(
                        older.Version < newer.Version,
                        $"{older.Version} (ABI {older.Abi}) outranks {newer.Version} (ABI {newer.Abi}); "
                        + "a server on the newer ABI would install the older build.");
                }
            }
        }
    }

    private static string MetaTargetAbi
    {
        get
        {
            using var meta = JsonDocument.Parse(File.ReadAllText(MetaJsonPath));
            return meta.RootElement.GetProperty("targetAbi").GetString()!;
        }
    }

    private static List<(Version Version, Version Abi)> ManifestVersions()
    {
        using var meta = JsonDocument.Parse(File.ReadAllText(MetaJsonPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "manifest.json")));

        var guid = meta.RootElement.GetProperty("guid").GetString();
        var plugin = manifest.RootElement.EnumerateArray().First(p => p.GetProperty("guid").GetString() == guid);

        // Materialised before the JsonDocument is disposed.
        return plugin.GetProperty("versions").EnumerateArray()
            .Select(v => (
                Version.Parse(v.GetProperty("version").GetString()!),
                Version.Parse(v.GetProperty("targetAbi").GetString()!)))
            .ToList();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "manifest.json")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the repository root from the test output directory.");
        return dir!.FullName;
    }
}

using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

/// <summary>
/// Terse factories for the <see cref="MediaStream"/> shapes the selection logic cares about.
/// </summary>
public static class MediaStreamBuilder
{
    public static MediaStream Audio(
        int index,
        string? language,
        int? channels = null,
        string? title = null) => new()
    {
        Index = index,
        Type = MediaStreamType.Audio,
        Language = language,
        Channels = channels,
        Title = title,
    };

    public static MediaStream Subtitle(
        int index,
        string? language,
        bool isForced = false,
        string? title = null) => new()
    {
        Index = index,
        Type = MediaStreamType.Subtitle,
        Language = language,
        IsForced = isForced,
        Title = title,
    };

    public static MediaStream Video(int index) => new()
    {
        Index = index,
        Type = MediaStreamType.Video,
    };
}

using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LanguageFailover.Services;

/// <summary>
/// Source-generated logging for <see cref="PlaybackStartHandler"/>.
/// <para>
/// These exist instead of direct <c>ILogger.LogInformation(...)</c> calls so that the
/// message templates are compiled once and the arguments are not boxed into a
/// <c>params object?[]</c> on every playback (CA1848 / CA1873). Keeping them in one
/// file also puts every message the plugin can emit in a single place.
/// </para>
/// <para>
/// Arguments are still evaluated by the caller, so a call site that has to build a
/// string — <c>string.Join</c> over a preference list — guards itself with
/// <c>ILogger.IsEnabled</c> when the level is one that is normally switched off.
/// </para>
/// </summary>
public partial class PlaybackStartHandler
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Language Failover: Using series override for '{SeriesName}' — Audio=[{Audio}], Subtitle=[{Sub}]")]
    private partial void LogSeriesOverride(string seriesName, string audio, string sub);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Language Failover: No streams found for item {ItemId}")]
    private partial void LogNoStreams(Guid itemId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Language Failover: Processing '{ItemName}' for user {UserKey} — Audio=[{Audio}], Subtitle=[{Sub}]")]
    private partial void LogProcessing(string? itemName, string userKey, string audio, string sub);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Language Failover: Error processing playback start event")]
    private partial void LogUnhandledError(Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Language Failover: Selected original-version audio stream at index {Index} for '{ItemName}'")]
    private partial void LogSelectedOriginalAudio(int index, string? itemName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Language Failover: No audio stream selected for '{ItemName}' with preferences [{Langs}]; assuming the client's default track (lang={Lang})")]
    private partial void LogNoAudioSelected(string? itemName, string langs, string lang);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Language Failover: Setting audio stream to index {Index} (lang={Lang}) for '{ItemName}'")]
    private partial void LogSettingAudio(int index, string lang, string? itemName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Language Failover: Audio is in '{Lang}' — selecting forced subtitle stream at index {Index} for '{ItemName}'")]
    private partial void LogSelectingForcedSubtitle(string? lang, int index, string? itemName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Language Failover: Audio '{Lang}' outranks every available subtitle language, disabling subtitles for '{ItemName}'")]
    private partial void LogDisablingSubtitles(string? lang, string? itemName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Language Failover: No matching subtitle stream for '{ItemName}' with preferences [{Langs}]")]
    private partial void LogNoSubtitleMatch(string? itemName, string langs);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Language Failover: Setting subtitle stream to index {Index} for '{ItemName}'")]
    private partial void LogSettingSubtitle(int index, string? itemName);
}

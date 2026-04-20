using Jellyfin.Plugin.LanguageFailover.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LanguageFailover.Services;

/// <summary>
/// Handles playback start events to enforce per-user language preferences.
/// </summary>
public class PlaybackStartHandler : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ISessionManager _sessionManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILocalizationManager _localizationManager;
    private readonly ILogger<PlaybackStartHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStartHandler"/> class.
    /// </summary>
    public PlaybackStartHandler(
        ISessionManager sessionManager,
        IMediaSourceManager mediaSourceManager,
        ILocalizationManager localizationManager,
        ILogger<PlaybackStartHandler> logger)
    {
        _sessionManager = sessionManager;
        _mediaSourceManager = mediaSourceManager;
        _localizationManager = localizationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackStartEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.Item is null || eventArgs.Session is null)
            {
                return;
            }

            if (eventArgs.Users is null || eventArgs.Users.Count == 0)
            {
                return;
            }

            var config = Plugin.Instance?.Configuration;
            if (config is null)
            {
                return;
            }

            var userId = eventArgs.Users[0].Id;
            var userKey = userId.ToString("N");

            var prefs = config.UserPreferences.Find(p => string.Equals(p.UserId, userKey, StringComparison.OrdinalIgnoreCase));
            if (prefs is null || !prefs.Enabled)
            {
                return;
            }

            // Check for series-specific overrides
            var audioLangs = prefs.AudioLanguages;
            var subtitleLangs = prefs.SubtitleLanguages;

            if (eventArgs.Item is Episode episode && episode.Series is not null)
            {
                var seriesKey = episode.Series.Id.ToString("N");
                var seriesOverride = prefs.SeriesOverrides.Find(
                    o => string.Equals(o.SeriesId, seriesKey, StringComparison.OrdinalIgnoreCase));

                if (seriesOverride is not null)
                {
                    if (seriesOverride.AudioLanguages.Count > 0)
                    {
                        audioLangs = seriesOverride.AudioLanguages;
                    }

                    if (seriesOverride.SubtitleLanguages.Count > 0)
                    {
                        subtitleLangs = seriesOverride.SubtitleLanguages;
                    }

                    _logger.LogInformation(
                        "Language Failover: Using series override for '{SeriesName}' — Audio=[{Audio}], Subtitle=[{Sub}]",
                        seriesOverride.SeriesName,
                        string.Join(", ", audioLangs),
                        string.Join(", ", subtitleLangs));
                }
            }

            if (audioLangs.Count == 0 && subtitleLangs.Count == 0)
            {
                return;
            }

            var itemId = eventArgs.Item.Id;
            var streams = _mediaSourceManager.GetMediaStreams(itemId);
            if (streams.Count == 0)
            {
                _logger.LogDebug("Language Failover: No streams found for item {ItemId}", itemId);
                return;
            }

            _logger.LogDebug(
                "Language Failover: Processing '{ItemName}' for user {UserKey} — Audio=[{Audio}], Subtitle=[{Sub}]",
                eventArgs.Item.Name,
                userKey,
                string.Join(", ", audioLangs),
                string.Join(", ", subtitleLangs));

            // Build an effective prefs object with potentially overridden languages
            var effectivePrefs = new UserLanguagePreference
            {
                AudioLanguages = audioLangs.ToList(),
                SubtitleLanguages = subtitleLangs.ToList(),
                PreferNonForcedSubtitles = prefs.PreferNonForcedSubtitles,
                Enabled = true
            };

            var sessionId = eventArgs.Session.Id;

            // Wait for the client player to be fully initialized before sending commands
            await Task.Delay(1500).ConfigureAwait(false);

            // Audio stream selection — returns the language of the selected audio stream
            var selectedAudioLang = await TrySetAudioStream(streams, effectivePrefs, sessionId, eventArgs.Item.Name).ConfigureAwait(false);

            // Small delay to let the client process the audio change before sending subtitle command
            await Task.Delay(500).ConfigureAwait(false);

            // Subtitle stream selection — uses audio language to decide behavior
            await TrySetSubtitleStream(streams, effectivePrefs, sessionId, eventArgs.Item.Name, selectedAudioLang).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Language Failover: Error processing playback start event");
        }
    }

    /// <summary>
    /// Returns the language code of the selected audio stream, or null.
    /// </summary>
    private async Task<string?> TrySetAudioStream(
        IReadOnlyList<MediaBrowser.Model.Entities.MediaStream> streams,
        UserLanguagePreference prefs,
        string sessionId,
        string? itemName)
    {
        if (prefs.AudioLanguages.Count == 0)
        {
            return null;
        }

        var bestAudioIndex = LanguageHelper.SelectBestAudioStream(streams, prefs.AudioLanguages, _localizationManager);
        if (bestAudioIndex is null)
        {
            _logger.LogDebug(
                "Language Failover: No matching audio stream for '{ItemName}' with preferences [{Langs}]",
                itemName,
                string.Join(", ", prefs.AudioLanguages));
            return null;
        }

        var selectedStream = streams.FirstOrDefault(s => s.Index == bestAudioIndex.Value);
        var selectedLang = selectedStream?.Language;

        _logger.LogInformation(
            "Language Failover: Setting audio stream to index {Index} (lang={Lang}) for '{ItemName}'",
            bestAudioIndex.Value,
            selectedLang ?? "unknown",
            itemName);

        var command = new GeneralCommand
        {
            Name = GeneralCommandType.SetAudioStreamIndex,
            Arguments = { ["Index"] = bestAudioIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };

        await _sessionManager.SendGeneralCommand(
            string.Empty,
            sessionId,
            command,
            CancellationToken.None).ConfigureAwait(false);

        return selectedLang;
    }

    private async Task TrySetSubtitleStream(
        IReadOnlyList<MediaBrowser.Model.Entities.MediaStream> streams,
        UserLanguagePreference prefs,
        string sessionId,
        string? itemName,
        string? selectedAudioLang)
    {
        if (prefs.SubtitleLanguages.Count == 0)
        {
            return;
        }

        // If audio is already in one of the preferred subtitle languages, skip subtitles
        if (!string.IsNullOrEmpty(selectedAudioLang))
        {
            foreach (var subLang in prefs.SubtitleLanguages)
            {
                if (LanguageHelper.LanguageMatches(selectedAudioLang, subLang, _localizationManager))
                {
                    _logger.LogInformation(
                        "Language Failover: Audio is already in subtitle language '{Lang}', disabling subtitles for '{ItemName}'",
                        subLang,
                        itemName);

                    var disableCmd = new GeneralCommand
                    {
                        Name = GeneralCommandType.SetSubtitleStreamIndex,
                        Arguments = { ["Index"] = "-1" }
                    };

                    await _sessionManager.SendGeneralCommand(
                        string.Empty,
                        sessionId,
                        disableCmd,
                        CancellationToken.None).ConfigureAwait(false);

                    return;
                }
            }
        }

        // Audio is NOT in a subtitle language — we want subtitles.
        // Accept forced subtitles if no non-forced are available.
        var bestSubIndex = LanguageHelper.SelectBestSubtitleStream(
            streams,
            prefs.SubtitleLanguages,
            prefs.PreferNonForcedSubtitles,
            _localizationManager);

        if (bestSubIndex is null)
        {
            _logger.LogDebug(
                "Language Failover: No matching subtitle stream for '{ItemName}' with preferences [{Langs}]",
                itemName,
                string.Join(", ", prefs.SubtitleLanguages));
            return;
        }

        _logger.LogInformation(
            "Language Failover: Setting subtitle stream to index {Index} for '{ItemName}'",
            bestSubIndex.Value,
            itemName);

        var command = new GeneralCommand
        {
            Name = GeneralCommandType.SetSubtitleStreamIndex,
            Arguments = { ["Index"] = bestSubIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };

        await _sessionManager.SendGeneralCommand(
            string.Empty,
            sessionId,
            command,
            CancellationToken.None).ConfigureAwait(false);
    }
}

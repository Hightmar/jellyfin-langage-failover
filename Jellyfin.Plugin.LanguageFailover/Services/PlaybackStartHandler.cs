using System.Globalization;
using Jellyfin.Plugin.LanguageFailover.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LanguageFailover.Services;

/// <summary>
/// Handles playback start events to enforce per-user language preferences.
/// </summary>
public class PlaybackStartHandler : IEventConsumer<PlaybackStartEventArgs>
{
    /// <summary>
    /// How long to wait after PlaybackStart before touching the client's track selection.
    /// Some clients (notably TV apps) are not done initialising their player when the event
    /// fires and silently revert commands that arrive too early. Do not lower without
    /// testing on a real TV client.
    /// </summary>
    private static readonly TimeSpan PlayerInitDelay = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// How long to wait between the audio and subtitle commands, so the client has finished
    /// applying the audio switch before the subtitle one arrives.
    /// </summary>
    private static readonly TimeSpan BetweenCommandsDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The stream index Jellyfin clients interpret as "no subtitle track".
    /// </summary>
    private const int SubtitlesDisabledIndex = -1;

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
                PreferOriginalAudio = prefs.PreferOriginalAudio,
                PreferForcedWhenAudioMatches = prefs.PreferForcedWhenAudioMatches
            };

            var sessionId = eventArgs.Session.Id;

            await Task.Delay(PlayerInitDelay).ConfigureAwait(false);

            // Audio stream selection — returns the language of the selected audio stream
            var selectedAudioLang = await TrySetAudioStream(streams, effectivePrefs, sessionId, eventArgs.Item.Name).ConfigureAwait(false);

            await Task.Delay(BetweenCommandsDelay).ConfigureAwait(false);

            // Subtitle stream selection — uses audio language to decide behavior
            await TrySetSubtitleStream(streams, effectivePrefs, sessionId, eventArgs.Item.Name, selectedAudioLang).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Language Failover: Error processing playback start event");
        }
    }

    /// <summary>
    /// Sends a track-selection command to the client. Index -1 disables the track.
    /// </summary>
    private Task SendStreamIndexCommand(GeneralCommandType commandType, int index, string sessionId)
    {
        var command = new GeneralCommand
        {
            Name = commandType,
            Arguments = { ["Index"] = index.ToString(CultureInfo.InvariantCulture) }
        };

        return _sessionManager.SendGeneralCommand(string.Empty, sessionId, command, CancellationToken.None);
    }

    /// <summary>
    /// Returns the language code of the selected audio stream, or null.
    /// </summary>
    private async Task<string?> TrySetAudioStream(
        IReadOnlyList<MediaStream> streams,
        UserLanguagePreference prefs,
        string sessionId,
        string? itemName)
    {
        int? bestAudioIndex = null;

        // If user prefers original version, try to find an audio stream tagged as "original" first
        if (prefs.PreferOriginalAudio)
        {
            bestAudioIndex = LanguageHelper.SelectOriginalAudioStream(streams);
            if (bestAudioIndex is not null)
            {
                _logger.LogInformation(
                    "Language Failover: Selected original-version audio stream at index {Index} for '{ItemName}'",
                    bestAudioIndex.Value,
                    itemName);
            }
        }

        if (bestAudioIndex is null)
        {
            if (prefs.AudioLanguages.Count == 0)
            {
                return null;
            }

            bestAudioIndex = LanguageHelper.SelectBestAudioStream(streams, prefs.AudioLanguages, _localizationManager);
        }

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

        await SendStreamIndexCommand(GeneralCommandType.SetAudioStreamIndex, bestAudioIndex.Value, sessionId)
            .ConfigureAwait(false);

        return selectedLang;
    }

    private async Task TrySetSubtitleStream(
        IReadOnlyList<MediaStream> streams,
        UserLanguagePreference prefs,
        string sessionId,
        string? itemName,
        string? selectedAudioLang)
    {
        if (prefs.SubtitleLanguages.Count == 0)
        {
            return;
        }

        // If audio is already in one of the preferred subtitle languages, either skip subtitles
        // entirely or switch to forced subtitles (useful for translating foreign dialog).
        if (!string.IsNullOrEmpty(selectedAudioLang))
        {
            foreach (var subLang in prefs.SubtitleLanguages)
            {
                if (!LanguageHelper.LanguageMatches(selectedAudioLang, subLang, _localizationManager))
                {
                    continue;
                }

                if (prefs.PreferForcedWhenAudioMatches)
                {
                    var forcedIdx = LanguageHelper.SelectForcedSubtitleForLanguage(
                        streams,
                        subLang,
                        _localizationManager);

                    if (forcedIdx is not null)
                    {
                        _logger.LogInformation(
                            "Language Failover: Audio is in '{Lang}' — selecting forced subtitle stream at index {Index} for '{ItemName}'",
                            subLang,
                            forcedIdx.Value,
                            itemName);

                        await SendStreamIndexCommand(GeneralCommandType.SetSubtitleStreamIndex, forcedIdx.Value, sessionId)
                            .ConfigureAwait(false);

                        return;
                    }
                }

                _logger.LogInformation(
                    "Language Failover: Audio is already in subtitle language '{Lang}', disabling subtitles for '{ItemName}'",
                    subLang,
                    itemName);

                await SendStreamIndexCommand(GeneralCommandType.SetSubtitleStreamIndex, SubtitlesDisabledIndex, sessionId)
                    .ConfigureAwait(false);

                return;
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

        await SendStreamIndexCommand(GeneralCommandType.SetSubtitleStreamIndex, bestSubIndex.Value, sessionId)
            .ConfigureAwait(false);
    }
}

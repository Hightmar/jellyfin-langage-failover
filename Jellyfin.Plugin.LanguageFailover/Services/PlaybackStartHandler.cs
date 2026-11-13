using System.Globalization;
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
public partial class PlaybackStartHandler : IEventConsumer<PlaybackStartEventArgs>
{
    /// <summary>
    /// Some clients (notably TV apps) revert commands that arrive before their player is ready.
    /// Do not lower without testing on a real TV client.
    /// </summary>
    private static readonly TimeSpan PlayerInitDelay = TimeSpan.FromMilliseconds(1500);

    private static readonly TimeSpan BetweenCommandsDelay = TimeSpan.FromMilliseconds(500);

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

            var userKey = eventArgs.Users[0].Id.ToString("N");

            var prefs = config.UserPreferences.Find(p => string.Equals(p.UserId, userKey, StringComparison.OrdinalIgnoreCase));
            if (prefs is null || !prefs.Enabled)
            {
                return;
            }

            SeriesLanguageOverride? seriesOverride = null;
            if (eventArgs.Item is Episode episode && episode.Series is not null)
            {
                seriesOverride = TrackSelectionPlanner.FindSeriesOverride(prefs, episode.Series.Id);
            }

            var effectivePrefs = TrackSelectionPlanner.ApplySeriesOverride(prefs, seriesOverride);

            if (seriesOverride is not null && _logger.IsEnabled(LogLevel.Information))
            {
                LogSeriesOverride(
                    seriesOverride.SeriesName,
                    string.Join(", ", effectivePrefs.AudioLanguages),
                    string.Join(", ", effectivePrefs.SubtitleLanguages));
            }

            if (effectivePrefs.AudioLanguages.Count == 0 && effectivePrefs.SubtitleLanguages.Count == 0)
            {
                return;
            }

            // An item with several versions has one BaseItem per file, and the session may be on
            // another one than eventArgs.Item. MediaSourceId is not a Guid for live streams.
            var streamOwnerId = Guid.TryParse(eventArgs.MediaSourceId, out var mediaSourceId) && mediaSourceId != Guid.Empty
                ? mediaSourceId
                : eventArgs.Item.Id;

            var streams = _mediaSourceManager.GetMediaStreams(streamOwnerId);
            if (streams.Count == 0 && streamOwnerId != eventArgs.Item.Id)
            {
                streams = _mediaSourceManager.GetMediaStreams(eventArgs.Item.Id);
            }

            if (streams.Count == 0)
            {
                LogNoStreams(eventArgs.Item.Id);
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogProcessing(
                    eventArgs.Item.Name,
                    userKey,
                    string.Join(", ", effectivePrefs.AudioLanguages),
                    string.Join(", ", effectivePrefs.SubtitleLanguages));
            }

            var plan = TrackSelectionPlanner.Plan(
                streams,
                effectivePrefs,
                eventArgs.Item.GetInheritedOriginalLanguage(),
                _localizationManager);

            var sessionId = eventArgs.Session.Id;
            var itemName = eventArgs.Item.Name;

            await Task.Delay(PlayerInitDelay).ConfigureAwait(false);

            await ApplyAudio(plan, effectivePrefs, sessionId, itemName).ConfigureAwait(false);

            await Task.Delay(BetweenCommandsDelay).ConfigureAwait(false);

            await ApplySubtitles(plan, effectivePrefs, sessionId, itemName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnhandledError(ex);
        }
    }

    private async Task ApplyAudio(TrackSelectionPlan plan, UserLanguagePreference prefs, string sessionId, string? itemName)
    {
        if (plan.AudioIndex is not int index)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogNoAudioSelected(itemName, string.Join(", ", prefs.AudioLanguages), plan.AudioLanguage ?? "unknown");
            }

            return;
        }

        if (plan.OriginalSignal is not null)
        {
            LogSelectedOriginalAudio(index, itemName, plan.OriginalSignal);
        }

        LogSettingAudio(index, plan.AudioLanguage ?? "unknown", itemName);

        await SendStreamIndexCommand(GeneralCommandType.SetAudioStreamIndex, index, sessionId).ConfigureAwait(false);
    }

    private async Task ApplySubtitles(TrackSelectionPlan plan, UserLanguagePreference prefs, string sessionId, string? itemName)
    {
        switch (plan.Subtitle)
        {
            case SubtitleAction.SelectForced when plan.SubtitleIndex is int forced:
                LogSelectingForcedSubtitle(plan.AudioLanguage, forced, itemName);
                await SendStreamIndexCommand(GeneralCommandType.SetSubtitleStreamIndex, forced, sessionId).ConfigureAwait(false);
                break;

            case SubtitleAction.Disable:
                LogDisablingSubtitles(plan.AudioLanguage, itemName);
                await SendStreamIndexCommand(GeneralCommandType.SetSubtitleStreamIndex, SubtitlesDisabledIndex, sessionId).ConfigureAwait(false);
                break;

            case SubtitleAction.Select when plan.SubtitleIndex is int index:
                LogSettingSubtitle(index, itemName);
                await SendStreamIndexCommand(GeneralCommandType.SetSubtitleStreamIndex, index, sessionId).ConfigureAwait(false);
                break;

            case SubtitleAction.NoMatch when _logger.IsEnabled(LogLevel.Debug):
                LogNoSubtitleMatch(itemName, string.Join(", ", prefs.SubtitleLanguages));
                break;
        }
    }

    private Task SendStreamIndexCommand(GeneralCommandType commandType, int index, string sessionId)
    {
        var command = new GeneralCommand
        {
            Name = commandType,
            Arguments = { ["Index"] = index.ToString(CultureInfo.InvariantCulture) }
        };

        return _sessionManager.SendGeneralCommand(string.Empty, sessionId, command, CancellationToken.None);
    }
}

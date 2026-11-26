using System.Globalization;
using System.Reflection;
using Jellyfin.Plugin.LanguageFailover.Configuration;
using Jellyfin.Plugin.LanguageFailover.Services;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

public class PlaybackAudioTests
{
    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(0, 0, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, null, true)]
    [InlineData(null, 2, false)]
    public async Task SendsAudioCommandOnlyWhenSelectionIsNeeded(int? plannedIndex, int? currentIndex, bool expectCommand)
    {
        var sessionManager = DispatchProxy.Create<ISessionManager, RecordingSessionManager>();
        var recording = (RecordingSessionManager)sessionManager;
        var handler = new PlaybackStartHandler(sessionManager, null!, new FakeLocalizationManager(), NullLogger<PlaybackStartHandler>.Instance);
        var plan = new TrackSelectionPlan(plannedIndex, "eng", null, SubtitleAction.None, null);

        await handler.ApplyAudio(plan, new UserLanguagePreference(), "session", null, currentIndex);

        if (!expectCommand)
        {
            Assert.Empty(recording.Commands);
            return;
        }

        var (sessionId, command) = Assert.Single(recording.Commands);
        Assert.Equal("session", sessionId);
        Assert.Equal(GeneralCommandType.SetAudioStreamIndex, command.Name);
        Assert.Equal(plannedIndex.GetValueOrDefault().ToString(CultureInfo.InvariantCulture), command.Arguments["Index"]);
    }

    public class RecordingSessionManager : DispatchProxy
    {
        public List<(string SessionId, GeneralCommand Command)> Commands { get; } = new();

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ISessionManager.SendGeneralCommand)
                || args is not [_, string sessionId, GeneralCommand command, _])
            {
                throw new NotSupportedException(targetMethod?.Name);
            }

            Commands.Add((sessionId, command));
            return Task.CompletedTask;
        }
    }
}

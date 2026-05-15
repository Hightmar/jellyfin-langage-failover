# Jellyfin Language Failover

[![Build](https://github.com/Hightmar/jellyfin-langage-failover/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/Hightmar/jellyfin-langage-failover/actions/workflows/build.yml)
[![Release](https://github.com/Hightmar/jellyfin-langage-failover/actions/workflows/release.yml/badge.svg)](https://github.com/Hightmar/jellyfin-langage-failover/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/Hightmar/jellyfin-langage-failover?color=blue)](https://github.com/Hightmar/jellyfin-langage-failover/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Hightmar/jellyfin-langage-failover/total?color=green)](https://github.com/Hightmar/jellyfin-langage-failover/releases)
[![Jellyfin](https://img.shields.io/badge/jellyfin-10.11.x-purple)](https://jellyfin.org)
[![License](https://img.shields.io/github/license/Hightmar/jellyfin-langage-failover)](LICENSE)

A Jellyfin plugin that provides per-user audio and subtitle language selection with priority-based fallback and per-series overrides.

Jellyfin only supports a single default audio/subtitle language per user. This plugin adds ordered language priority lists, so the best available track is automatically selected on every playback start — including when binge-watching episodes on TV apps that don't preserve manual selections.

## Features

- **Ordered language priorities** — Define a ranked list of preferred languages for audio and subtitles (e.g., Audio: Chinese > Korean > Japanese > English > French). The first available match wins. Reorder by drag-and-drop or with the up/down arrows.
- **Prefer original audio (VO)** — Optional toggle that picks the track tagged as "Original" / "VO" / "Version Originale" over the priority list. Useful when watching a foreign film in its original language regardless of dubs available.
- **Smart subtitle behavior** — Subtitles are automatically enabled only when the audio is not in your subtitle language. If you set subtitles to French and the audio is already French, subtitles are disabled.
- **Forced subtitles when audio matches your language** — Optional toggle: instead of disabling subtitles when audio is already in your language, keep forced subtitles on to translate foreign dialog or signage.
- **Forced subtitle fallback** — Prefers complete (non-forced) subtitles, but falls back to forced subtitles when they're the only option available.
- **Per-series overrides** — Override language preferences for specific series. For example, keep your global preferences but force a specific anime to always play with its original audio.
- **Per-user configuration** — Each user has independent language preferences and series overrides.
- **Works with all clients** — Web, Android, iOS, TV apps, Kodi — commands are sent via WebSocket so any client that supports `GeneralCommand` will respond.
- **Automatic per-episode** — No need to manually re-select tracks between episodes. The plugin fires on every `PlaybackStart` event.

## How It Works

1. When playback starts, the plugin intercepts the `PlaybackStart` event
2. It looks up the user's configured language priorities
3. If the item is a TV episode, it checks for series-specific overrides
4. It analyzes all available audio/subtitle streams on the media file
5. **Audio selection**: if "Prefer original audio" is on and a track is tagged as Original/VO, it wins; otherwise the highest-priority language match wins (surround preferred over stereo within the same language)
6. **Subtitle selection**: if the selected audio is in one of your subtitle languages, subtitles are either disabled or switched to forced (when "Show forced subtitles when audio matches" is on); otherwise the best subtitle track in your priority order is selected
7. It sends `SetAudioStreamIndex` / `SetSubtitleStreamIndex` commands to the client

There is a brief moment (~1-2 seconds) with the default tracks before the plugin's selection takes effect. This is inherent to the plugin architecture — Jellyfin does not provide a hook to intercept track selection before playback begins.

## Installation

### Via Jellyfin Plugin Repository (recommended)

1. In Jellyfin, go to **Dashboard > Plugins > Repositories**
2. Click **+** to add a new repository
3. Enter:
   - **Repository Name**: `Language Failover`
   - **Repository URL**: `https://raw.githubusercontent.com/Hightmar/jellyfin-langage-failover/main/manifest.json`
4. Click **Save**
5. Go to **Dashboard > Plugins > Catalog**
6. Find **Language Failover** under "General"
7. Click **Install** and select the latest version
8. Restart Jellyfin

Future updates will appear automatically in the catalog.

### Manual Installation from Release

1. Download the latest `language-failover_*.zip` from the [Releases page](https://github.com/Hightmar/jellyfin-langage-failover/releases)
2. Extract to your Jellyfin plugins directory:
   - **Linux**: `/var/lib/jellyfin/plugins/LanguageFailover/`
   - **Windows**: `%LOCALAPPDATA%\jellyfin\plugins\LanguageFailover\`
   - **Docker**: `/config/plugins/LanguageFailover/`
3. The folder should contain:
   - `Jellyfin.Plugin.LanguageFailover.dll`
   - `meta.json`
4. Restart Jellyfin

### From Source

```bash
git clone https://github.com/Hightmar/jellyfin-langage-failover.git
cd jellyfin-langage-failover
dotnet build Jellyfin.Plugin.LanguageFailover --configuration Release
```

Copy `bin/Release/net9.0/Jellyfin.Plugin.LanguageFailover.dll` and `Jellyfin.Plugin.LanguageFailover/meta.json` to the plugin directory, then restart Jellyfin.

## Configuration

Navigate to **Dashboard > Plugins > Language Failover**.

### Global Preferences

1. **Select a user** from the dropdown
2. **Enable/disable** Language Failover for that user
3. **Audio Language Priority** — Add languages and reorder them by drag-and-drop (or with the up/down arrows). The first available match in the media file wins. Among streams of the same language, surround sound (5.1/7.1) is preferred over stereo.
4. **Subtitle Language Priority** — Same principle. Subtitles are only activated if the selected audio is not already in one of the subtitle languages.
5. **Prefer non-forced subtitles** — When checked, complete subtitles are preferred over forced (signs/songs only) subtitles. If only forced subtitles are available, they will still be selected as a fallback.
6. **Prefer original audio (VO)** — When checked, a track tagged as Original / VO / Version Originale beats the priority list. Falls back to the priority list if no track is tagged.
7. **Show forced subtitles when audio matches your language** — When checked, instead of disabling subtitles in the case where audio already matches one of your subtitle languages, the plugin selects a forced subtitle track in that language if one exists. Useful for translating foreign dialog or on-screen text in an otherwise native-language film.
8. **Save**

### Series Overrides

Below the global preferences, you can add per-series overrides:

1. **Search** for a series by name in the search box
2. **Click** on the series in the results to add it
3. **Configure** audio and/or subtitle languages specifically for that series
4. Leave a field empty to fall back to the global preferences for that field

**Example use case**: Your global audio priority is `Chinese > Korean > Japanese > English > French`, but for a specific French-dubbed anime you want `French` audio only — add a series override with Audio set to `French`.

## Supported Languages

The configuration page loads the full language list from Jellyfin's localization API at runtime, so every language your server recognises is selectable.

The plugin uses ISO 639 language matching with cross-format support (ISO 639-1 two-letter codes like `fr` match ISO 639-2 three-letter codes like `fra`/`fre`), so it works regardless of how your media files are tagged.

## Requirements

- Jellyfin Server **10.11.x**
- .NET 9.0 SDK (for building from source only)

## Troubleshooting

### The plugin doesn't change tracks

- Verify the plugin is loaded: check Jellyfin logs for `Loaded plugin: Language Failover`
- Verify the user has preferences configured and the plugin is enabled for that user
- Enable debug logging in Jellyfin (`Logging:LogLevel:Jellyfin.Plugin.LanguageFailover` = `Debug`) to see detailed selection info

### Tracks change but revert immediately

Some clients may override the plugin's selection with their own defaults. The plugin sends commands after a short delay to mitigate this, but some clients may not support `GeneralCommand` for stream switching.

### Series override not working

- Make sure you clicked **Save** after adding the override and configuring its languages
- The override only applies to **episodes** of the series, not movies
- Check the logs with debug level to verify the series ID matches

## Releasing (maintainers)

Releases are automated via GitHub Actions. To publish a new version:

```bash
git tag v1.1.0.1 -m "Release 1.1.0.1"
git push origin v1.1.0.1
```

The workflow will:
1. Build the plugin in Release mode
2. Package the DLL and meta.json as `language-failover_<version>.zip`
3. Create a GitHub Release with the ZIP attached
4. Update `manifest.json` on `main` with the new version entry and commit it back

Users subscribed to the repository will see the new version in their Jellyfin plugin catalog.

Version numbers must follow the format `major.minor.patch.build` (four segments, as required by Jellyfin).

## License

This plugin is provided as-is under the MIT License.

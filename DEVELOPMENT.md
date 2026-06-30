# Development Notes

Internal notes for maintainers — context and gotchas that aren't obvious from the code.
Keep this updated when behaviour or workflow changes.

## Architecture overview

- **`Services/PlaybackStartHandler.cs`** — entry point. Subscribes to `PlaybackStartEventArgs`.
  Resolves the user's preferences (with per-series overrides), then selects and applies the
  audio track first, then the subtitle track, via `GeneralCommand`
  (`SetAudioStreamIndex` / `SetSubtitleStreamIndex`) over WebSocket.
  - Timing matters: there is a `Task.Delay(1500)` before sending commands (wait for the client
    player to init) and a `Task.Delay(500)` between audio and subtitle commands. Some clients
    revert selections if commands arrive too early. Don't remove these without testing on TV apps.
- **`Services/LanguageHelper.cs`** — pure, static selection logic. No I/O. This is where stream
  matching/scoring lives and where most behaviour bugs are fixed.
- **`Configuration/`** — `PluginConfiguration` holds a list of `UserLanguagePreference`, each with
  ordered `AudioLanguages` / `SubtitleLanguages`, toggles, and `SeriesOverrides`.
- **`Pages/configPage.{html,js}`** — admin UI. Language list is loaded at runtime from Jellyfin's
  localization API (`ApiClient.getCultures()`), with a small hardcoded fallback.

## Key behaviours / decisions

### Forced vs. complete subtitle detection (fixed in 1.1.0.2)

- **Do not rely on `MediaStream.IsForced` alone.** Many files never set the container forced
  disposition flag and only mark forced tracks in the stream **title** (`Forced`, `Forcé`, …).
  Relying on the flag caused forced tracks to be treated as complete and stay selected even when a
  real complete track existed.
- Use **`LanguageHelper.IsForcedSubtitle(stream)`** everywhere a forced/complete decision is made.
  It checks, in order:
  1. `stream.IsForced` (container flag) → forced.
  2. Explicit negation in the title (`non forcé`, `non-forced`, `not forced`) → **not** forced.
     This guard exists because `\bforced\b` would otherwise match the "forced" inside "non-forced".
  3. Forced keyword in `Title` **or** `DisplayTitle` → forced.
- Keyword coverage (regex `ForcedSubtitleRegex`): `forced`, `forcé/forcée/forcés/forcées`,
  `forzado(s)`, `forzato/forzati`, `erzwungen`. Add languages here if needed.
- **Known limitation:** a track that is genuinely forced but has neither the flag nor any keyword
  is indistinguishable from a complete track — Jellyfin's `MediaStream` does not expose cue count
  or track size. Fix at the source: `mkvpropedit file.mkv --edit track:sN --set flag-forced=1`.
- All three subtitle paths funnel through `IsForcedSubtitle`: `SelectBestSubtitleStream`
  (prefer-non-forced), `SelectForcedSubtitleForLanguage` (audio-matches case), and the fallback.
  When touching forced logic, update the helper, not the call sites.

### Language matching

- `LanguageHelper.LanguageMatches` cross-matches ISO 639-1 (2-letter) and ISO 639-2 (3-letter)
  codes via `ILocalizationManager.FindLanguageInfo`, in both directions. Don't assume media is
  tagged in a single code format.

### Audio selection

- Among same-language matches, **higher channel count wins** (surround over stereo).
- `PreferOriginalAudio` uses `OriginalVersionRegex` on the stream **title** (`original`, `VO`,
  `version originale`, …) and, when on, beats the priority list; falls back to the list if no
  tagged track exists.

## Build

```bash
dotnet build Jellyfin.Plugin.LanguageFailover --configuration Release
```

Output DLL: `Jellyfin.Plugin.LanguageFailover/bin/Release/net9.0/Jellyfin.Plugin.LanguageFailover.dll`
Deploy that DLL + `Jellyfin.Plugin.LanguageFailover/meta.json` to the plugin folder and restart Jellyfin.

## Branch & release workflow

- Work on **`develop`**; merge into **`main`** for releases. (Note: `README.md` on `main` has been
  ahead of `develop` in the past — when editing docs on `develop`, base them on `main`'s version to
  avoid losing content on merge.)
- Releases are automated by GitHub Actions on a tag push:
  ```bash
  git tag v1.1.0.2 -m "Release 1.1.0.2"
  git push origin v1.1.0.2
  ```
  The workflow builds, packages `language-failover_<version>.zip`, creates the GitHub Release, and
  updates `manifest.json` on `main` (adds the version entry with `sourceUrl` + `checksum`).
- Bump **`Jellyfin.Plugin.LanguageFailover/meta.json`** (`version`, `timestamp`, optionally
  `changelog`) before tagging. `manifest.json` is updated by the workflow — don't hand-edit its
  `sourceUrl`/`checksum`.
- Versions must be 4 segments `major.minor.patch.build` (Jellyfin requirement).

### Local commit note

GPG signing (`commit.gpgsign=true`) is configured but the signing key may not be present in every
environment; commits were made with `--no-gpg-sign` when no key was available (existing history is
also unsigned).

## Version history (behavioural)

- **1.1.0.2** — Fix: forced subtitles detected via title in addition to the `IsForced` flag, with a
  `non-forced` guard; README updated.
- **1.1.0.0 / 1.1.0.1** — Original-audio preference, forced-subtitle fallback, drag-drop reorder,
  dynamic language list from the localization API, UI redesign.

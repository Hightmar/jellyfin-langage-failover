# Development Notes

Internal notes for maintainers — context and gotchas that aren't obvious from the code.
Keep this updated when behaviour or workflow changes.

## Architecture overview

- **`Services/PlaybackStartHandler.cs`** — entry point. Subscribes to `PlaybackStartEventArgs`.
  Resolves the user's preferences (with per-series overrides), then selects and applies the
  audio track first, then the subtitle track, via `GeneralCommand`
  (`SetAudioStreamIndex` / `SetSubtitleStreamIndex`) over WebSocket. All commands go through
  `SendStreamIndexCommand`.
  - Timing matters: `PlayerInitDelay` (1500 ms) before sending anything and
    `BetweenCommandsDelay` (500 ms) between the audio and subtitle commands. Some clients
    revert selections if commands arrive too early. Don't lower these without testing on TV apps.
  - Streams are read for `eventArgs.MediaSourceId` when it parses as a Guid, falling back to
    `eventArgs.Item.Id`. An item with several versions has one `BaseItem` per file, so the
    session may be on a different one than `Item`; `MediaSourceId` is also not a Guid for live
    streams, hence the fallback.
- **`Services/LanguageHelper.cs`** — pure, static selection logic. No I/O. This is where stream
  matching/scoring lives and where most behaviour bugs are fixed.
- **`Configuration/`** — `PluginConfiguration` holds a list of `UserLanguagePreference`, each with
  ordered `AudioLanguages` / `SubtitleLanguages`, toggles, and `SeriesOverrides`.
- **`Pages/configPage.{html,js}`** — admin UI. Language list is loaded at runtime from Jellyfin's
  localization API (`ApiClient.getCultures()`), with a small hardcoded fallback.

## Tests

Two suites, both run in CI (`.github/workflows/build.yml`):

- **`Jellyfin.Plugin.LanguageFailover.Tests/`** (xUnit) covers `LanguageHelper` and plugin
  identity. `FakeLocalizationManager` implements `ILocalizationManager` over a small ISO table;
  every member the selection logic does not use throws, so a new dependency shows up as a test
  failure rather than silently passing.
  - `PluginIdentityTests` guards the GUID, which is restated in `Plugin.cs`, `meta.json`,
    `manifest.json` and `configPage.js` — three languages, no way to share one declaration.
    It also pins the four-segment version shape Jellyfin requires.
- **`web-tests/`** (node + jsdom) drives the real `configPage.html` / `configPage.js`, clicking
  through them the way an admin would and asserting what reaches
  `updatePluginConfiguration`. This is the only coverage the page can get — it never reaches
  the .NET suite. Run it with `cd web-tests && npm ci && npm test`.

`MediaStream.DisplayTitle` is **read-only** (the server composes it), so tests cannot set it;
`MediaStreamBuilder` only exposes what is writable.

## Key behaviours / decisions

### Forced vs. complete subtitle detection

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
- The `DisplayTitle` arm is a safety net, not an independent signal: the server composes
  `DisplayTitle` from `Title` plus the flags, so it can rarely say anything `Title` and
  `IsForced` did not. It is kept because it costs nothing. `SelectOriginalAudioStream`
  deliberately checks `Title` only, for the same reason — adding `DisplayTitle` there would be
  a no-op.
- **Known limitation:** a track that is genuinely forced but has neither the flag nor any keyword
  is indistinguishable from a complete track — Jellyfin's `MediaStream` does not expose cue count
  or track size. Fix at the source: `mkvpropedit file.mkv --edit track:sN --set flag-forced=1`.
- All subtitle paths funnel through `IsForcedSubtitle`. When touching forced logic, update the
  helper, not the call sites.

### When subtitles are suppressed

Subtitles are turned off (or reduced to forced) only when the audio language ranks **at least as
highly** in the user's subtitle list as the best subtitle language the file actually has —
`LanguageHelper.AudioMakesSubtitlesRedundant`.

The rule matters when the two lists disagree. With subtitles ranked `[en, fr]` and French audio:
English subtitles outrank French, so they are selected; but if the file carries no English
subtitles, French subtitles would only repeat the audio, so they are suppressed. The earlier
implementation suppressed on *any* match at *any* rank and got the first case wrong.

### Audio language when nothing is selected

If no audio stream is selected — no language match, or "prefer original audio" with no tagged
track and an empty preference list — the plugin sends no audio command and the client plays its
own default. `GetDefaultAudioLanguage` reports that track (`IsDefault`, else the first audio
stream) so the subtitle decision is made against what will actually be heard. Reporting "unknown"
instead used to switch subtitles on over audio the viewer already understood.

### Language matching

- `LanguageHelper.LanguageMatches` cross-matches ISO 639-1 (2-letter) and ISO 639-2 (3-letter)
  codes via `ILocalizationManager.FindLanguageInfo`, in both directions (`CultureKnowsCode` does
  one direction; `LanguageMatches` calls it twice). Don't assume media is tagged in a single
  code format.

### Audio selection

- Among same-language matches, **higher channel count wins** (surround over stereo).
- `PreferOriginalAudio` uses `OriginalVersionRegex` on the stream **title** (`original`, `VO`,
  `version originale`) and, when on, beats the priority list; falls back to the list if no
  tagged track exists. Note that `original` alone subsumes `original audio` / `original
  language` / `original version` — the `\b` after it matches before the following space — so
  don't re-add those alternatives.
- `SelectBestAudioStream` and `SelectBestSubtitleStream` share `SelectByLanguagePriority` and
  differ only in their tie-break. Language priority always wins; a tie-break never promotes a
  lower-priority language.

### Config page

- Language lists are addressed by their `<ul class="lf-chips">` element, found with `closest()`
  from the clicked button, with the `<select>` found beside it in the same `.lf-col`. There are
  no generated element ids and no encoded type strings — the global lists and the per-series
  override lists go through the same handlers. Keep that structural contract
  (`.lf-col > ul.lf-chips` + `.lf-col > .lf-add-row > select`) if you touch the markup.
- Each override block carries its own `data-series-id` / `data-series-name`. Never recover a
  series identity from the block's position in the DOM.
- Event listeners are bound to `view`, never to `document`: Jellyfin re-runs the controller on
  every navigation to the page, and a document-level listener would never be removed.
- **No hardcoded light/dark colours.** Text is `currentColor` at varying opacity, surfaces and
  borders are neutral grey overlays (`--lf-surface`, `--lf-border`); only the accent comes from
  the theme. Jellyfin ships light themes.
- The page is English-only by design. Jellyfin's plugin config pages have no i18n plumbing worth
  the machinery for a single language file; if translations are ever wanted, that is a
  deliberate project, not a drive-by change.

## Build

```bash
dotnet build Jellyfin.Plugin.LanguageFailover.sln --configuration Release
dotnet test  Jellyfin.Plugin.LanguageFailover.sln --configuration Release
cd web-tests && npm ci && npm test
```

Shared MSBuild properties live in `Directory.Build.props`; package versions are central in
`Directory.Packages.props`, so bumping Jellyfin is a one-line edit there (plus `targetAbi` in
`meta.json`). CI builds with `-warnaserror`, so keep the tree warning-clean.

**The analyzer set is a pinned NuGet package** (`Microsoft.CodeAnalysis.NetAnalyzers` in
`Directory.Packages.props`), not whatever the installed SDK ships, and `global.json` pins the
SDK feature band. This is deliberate: with `-warnaserror`, an SDK upgrade on the GitHub runner
would otherwise be able to introduce a new rule and break the build with no code change — which
is exactly what happened once, when the runner had CA1873 and the local SDK did not. Bump the
analyzer package on purpose, see what it finds, then commit.

All logging goes through source-generated `[LoggerMessage]` methods in
`Services/PlaybackStartHandler.Logging.cs`, which is what keeps CA1848 satisfied rather than
suppressed. Add new messages there; do not call `_logger.LogX(...)` directly. Call sites that
have to build a string (`string.Join` over a preference list) guard themselves with
`_logger.IsEnabled(...)`.

Output DLL: `Jellyfin.Plugin.LanguageFailover/bin/Release/net9.0/Jellyfin.Plugin.LanguageFailover.dll`
Deploy that DLL + `Jellyfin.Plugin.LanguageFailover/meta.json` to the plugin folder and restart Jellyfin.

## Branch & release workflow

- Work on **`develop`**; merge into **`main`** for releases. (Note: `README.md` on `main` has been
  ahead of `develop` in the past — when editing docs on `develop`, base them on `main`'s version to
  avoid losing content on merge.)
- Releases are automated by GitHub Actions on a tag push:
  ```bash
  git tag -a v1.1.0.3 -F release-notes.md --cleanup=verbatim
  git push origin v1.1.0.3
  ```
  The tag **must be annotated** (`-a`/`-F`): the release job refuses a lightweight tag
  rather than fall back to the commit message. GitKraken and similar GUIs often create
  lightweight tags — check with `git cat-file -t <tag>`, which must print `tag`.
  Note the `--cleanup=verbatim`: without it `git tag` strips every line starting with
  `#`, which silently eats Markdown headings from the release note.

  **The tag message becomes the release changelog** — in the GitHub release, in
  `manifest.json`, and in `meta.json`. Write it for users. (Earlier releases used the tagged
  commit's message, which is why the manifest history contains entries like
  "Merge branch 'develop'"; 1.2.0.0 is the last one affected.)
- The workflow builds, runs both test suites, packages `language-failover_<version>.zip`, creates
  the GitHub Release, and updates `manifest.json` + `meta.json` on `main`.
- **Don't hand-edit `manifest.json`.** `scripts/update_manifest.py` writes it, reading the plugin's
  identity (guid, name, description, overview, category, owner, targetAbi) from `meta.json` —
  `meta.json` is the single source of truth. `scripts/set_meta_version.py` stamps the version and
  timestamp and rejects anything that is not four segments `major.minor.patch.build`.
- Both scripts take their inputs from environment variables and never interpolate anything into
  their own source, so a tag message containing quotes, backslashes or `'''` is just data. Keep it
  that way.

### Local commit note

GPG signing (`commit.gpgsign=true`) is configured but the signing key may not be present in every
environment; commits were made with `--no-gpg-sign` when no key was available (existing history is
also unsigned).

## Version history (behavioural)

- **Unreleased** — Subtitle suppression now respects priority order instead of triggering on any
  match; the client's default audio track is used for the subtitle decision when no audio command
  is sent; streams are read from the played media source. Config page reworked around DOM
  structure and made theme-aware. Test suites added for both the selection logic and the config
  page. Release workflow hardened.
- **1.1.0.2** — Fix: forced subtitles detected via title in addition to the `IsForced` flag, with a
  `non-forced` guard; README updated.
- **1.1.0.0 / 1.1.0.1** — Original-audio preference, forced-subtitle fallback, drag-drop reorder,
  dynamic language list from the localization API, UI redesign.

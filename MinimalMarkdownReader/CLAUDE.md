# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

MinimalMarkdownReader is a single-document, read-only Markdown viewer for Windows 10/11 x64, built with .NET 10, WPF, WebView2 and Markdig. The UI supports Traditional Chinese and English, and the README and test plan are in Traditional Chinese. `AGENTS.md` has the repository conventions, and `history.md` has the version history and design decisions. Read both before making large changes.

The solution is `MinimalMarkdownReader.sln`.

## Commands

Run these from the repo root:

```powershell
dotnet restore MinimalMarkdownReader.sln            # add --ignore-failed-sources if offline; NU1900 warnings are harmless
dotnet build MinimalMarkdownReader.sln --no-restore --configuration Debug
dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug
dotnet test MinimalMarkdownReader.sln --filter "FullyQualifiedName~MarkdownServiceTests"   # single class/test
dotnet run --project src/MinimalMarkdownReader/MinimalMarkdownReader.csproj
```

To build the installer, use Inno Setup 6. It packages `artifacts/publish/` into `artifacts/installer/`:

```powershell
dotnet restore MinimalMarkdownReader.sln --runtime win-x64
dotnet publish src/MinimalMarkdownReader/MinimalMarkdownReader.csproj -c Release -r win-x64 --self-contained true --no-restore --output artifacts/publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/MinimalMarkdownReader.iss
```

For a release, bump `MyAppVersion` in `installer/MinimalMarkdownReader.iss` and record the version, installer size and SHA-256 in `history.md`. The installer does not bundle the WebView2 Runtime.

## Architecture

`MainWindow.xaml.cs` is the only orchestrator. It owns the services, holds the current `ReaderSettings`, and saves settings through a debounce timer and again on close. `App.xaml.cs` passes a file path from the command line (file association) to `MainWindow`. That file opens after WebView2 has initialized.

How a document is displayed:
1. `DocumentService.LoadAsync` accepts only `.md`/`.markdown` files and decodes them as strict UTF-8. Invalid bytes cause an error; there is no fallback encoding.
2. `MarkdownService.ToSafeHtml` runs Markdig with `UseAdvancedExtensions().DisableHtml()`. It then rewrites `<img>`/`<a>` tags with regexes:
   - Images go through `DocumentService.TryResolveRelativeImage`, which allows only existing raster files inside the document folder. Allowed images are rewritten to `https://markdown.localhost/...`; any other image becomes a localized "blocked" span.
   - Links that are not http(s) are reduced to a bare `<a>`.
3. `ReaderService.ShowDocument` maps the virtual host `markdown.localhost` to the document's folder, then calls `NavigateToString` with an HTML shell. The shell's CSS and JS are inline strings in `ReaderService.HtmlShell`; there are no separate resource files. After navigation completes, settings are pushed in through `window.reader.applySettings(json)`.
4. `ReaderService` intercepts navigation and new-window requests. It opens http(s) URLs in the default browser and blocks everything else.

Changing a setting does not re-render the document. `ApplySettingsAsync` only updates CSS variables through script. All theme colors are defined only in `Models/ThemePalettes.cs`. The JS palettes in `HtmlShell`, the CSS default variables, the `CustomColors` defaults and the theme validation all read from it. To add a theme, add one entry there plus a menu item in `MainWindow.xaml` and a localized string.

Settings:
- `SettingsService` reads and writes `%LOCALAPPDATA%\MinimalMarkdownReader\settings.json` atomically (it writes a temp file, then moves it).
- On first run it falls back to the legacy `%LOCALAPPDATA%\MarkdownReader\settings.json`.
- `SettingsService.Validate` defines the valid range for every field: an enum whitelist, numeric ranges, and `#RRGGBB` colors. Files that are invalid or corrupt load as defaults.
- Any new setting needs a validation rule, because its values get passed into CSS/JS.
- `ReaderSettings` stores stable machine values (`soft`, `eye`, `left`, `zh-Hant`, `en`), not display text.

Localization:
- `LocalizationService.Instance` is a singleton that services also use for their error messages. It is backed by `Resources/Strings.resx` (English) and `Strings.zh-Hant.resx`.
- To add a language:
  1. Add `Strings.<culture>.resx`.
  2. Add the culture to the whitelists in `LocalizationService.SetLanguage` and `SettingsService.Validate`.
  3. Add it to the language menu.

## Constraints

- Treat Markdown as untrusted. Raw HTML stays disabled; scripts, iframes, `javascript:` URIs, remote images and images outside the document folder must not load. Tests in `tests/MinimalMarkdownReader.Tests/` cover these rules, so extend them when you touch `MarkdownService`, `DocumentService` or `SettingsService`.
- Keep the app to one document and read-only unless a version explicitly changes that scope. Search, print/PDF, syntax highlighting and similar features are listed only as possible future work.
- In `MainWindow.xaml`, the reading `Grid` must stay the last child of the `DockPanel`. Otherwise the status bar covers the WebView2.
- `about.md` is copied next to the executable at build and publish time, and the About dialog reads it.
- Only report GUI/WebView2 checks that were actually run. Record manual checks and results in `docs/TEST_PLAN.md`; `samples/README.md` is the manual test document.

# Development History

## v1.0 — 2026-10-22

**Status:** Complete and manually accepted.

MinimalMarkdownReader is a Windows 10/11 x64, read-only Markdown viewer built with .NET 10, C#, WPF, Markdig, and WebView2. Open `MinimalMarkdownReader.sln` in Visual Studio. The application project is `src/MinimalMarkdownReader/`; xUnit tests are in `tests/MinimalMarkdownReader.Tests/`.

### Delivered functionality

- Opens one UTF-8 `.md` or `.markdown` file at a time via the menu, `Ctrl+O`, or drag and drop.
- Renders common Markdown in WebView2 with left, center, and right text alignment; code blocks remain left-aligned and scroll horizontally.
- Provides system-font selection, 12–36 px font size, 1.2–2.5 line height, soft/eye-care/light/dark/custom themes, and color-palette selection for custom colors.
- Uses a 95% reading width with small side margins.
- Saves global reading preferences, including UI language, to `%LOCALAPPDATA%\MinimalMarkdownReader\settings.json`; it imports the old MarkdownReader settings file if needed.
- Supports Traditional Chinese and English. Use the toolbar hamburger menu to open files, switch language, or open About.
- Reads About content from `src/MinimalMarkdownReader/about.md`, copied beside the executable during build and publish.

### Security and design decisions

- Markdown raw HTML is disabled. JavaScript, iframe content, unsafe URIs, remote images, and unsupported local resources are blocked.
- Only document-folder-relative PNG, JPG, GIF, WebP, and BMP images are allowed. HTTP(S) links open in the default browser.
- `ReaderSettings` stores stable machine values (`soft`, `eye`, `left`, `zh-Hant`, `en`); localized display strings are held in `Resources/Strings*.resx`.
- The WPF `DockPanel` must keep the reading `Grid` as its final child so the status bar cannot cover the WebView2 area.

### Verification

```powershell
dotnet restore MinimalMarkdownReader.sln --ignore-failed-sources
dotnet build MinimalMarkdownReader.sln --no-restore --configuration Debug
dotnet test MinimalMarkdownReader.sln --no-build --configuration Debug
```

Latest result: build succeeded; 9/9 tests passed. NuGet may issue `NU1900` when the environment cannot fetch vulnerability metadata; it does not prevent restoring cached packages or building.

### Installer package

- Inno Setup 6.7.3 is installed for the current user.
- `installer/MinimalMarkdownReader.iss` creates a per-user Windows x64 installer from `artifacts/publish/`.
- The 2026-10-22 release build used a self-contained .NET 10 x64 publish. Its installer is `artifacts/installer/MinimalMarkdownReader-Setup-1.0.0.exe` (53,992,907 bytes; SHA-256 `CA0B7C80EF34FDAE4BDCBA277EDEC5BB15A5AA3E4FB661B0580731E3E1D36130`).
- The installer does not bundle the Microsoft Edge WebView2 Runtime; Windows must provide that runtime before the reader can display documents.

## v1.0.1 — 2026-10-22

- Added startup argument handling. When Windows opens the reader for a `.md` or `.markdown` file association, `App` passes the file path to `MainWindow` and it opens after WebView2 initializes.
- Release build and 9/9 xUnit tests passed.
- Rebuilt the self-contained Windows x64 installer as `artifacts/installer/MinimalMarkdownReader-Setup-1.0.1.exe` (53,993,994 bytes; SHA-256 `1CC37ECECE75AC499DF8F1AEED4CFB9D5C07EDD9AAD87F6C4AE5FE9CBF3464ED`).

## v1.0.2 — 2026-10-22

- Added the multi-size `Assets/MinimalMarkdownReader.ico` as the executable and installer icon.
- The installer registers `MinimalMarkdownReader.Markdown` for `.md` and `.markdown`, including its default icon and open command.
- Release build and 9/9 xUnit tests passed. The self-contained Windows x64 installer is `artifacts/installer/MinimalMarkdownReader-Setup-1.0.2.exe` (54,208,579 bytes; SHA-256 `65A763AA7386BB90C7AF2C05CD640E441669D4050CB0E6D8CB3C0044779F6D1B`).

### Future version starting points

- Add further UI languages by creating `Resources/Strings.<culture>.resx` and adding the culture to `LocalizationService` and the language menu.
- Consider full-text search, print/PDF export, additional Markdown extensions, or optional syntax highlighting only as separate scoped features.
- Keep the single-document, read-only model and the existing Markdown resource safety restrictions unless a new version explicitly revisits them.

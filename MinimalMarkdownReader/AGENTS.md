# Repository Guidelines

## Project Structure & Modules

This repository is intended for a Windows Markdown reader built with .NET 10, C#, WPF, WebView2, and Markdig. Keep the Visual Studio solution at the root (`MinimalMarkdownReader.sln`), application code under `src/MinimalMarkdownReader/`, and manual or automated test material under `docs/` and `samples/`. Organize application responsibilities into focused areas such as `Models/`, `Services/`, `Views/`, and `Resources/` (HTML/CSS). Keep UI, document loading, Markdown conversion, rendering, and settings persistence separate.

## Build, Test & Run

Run these commands from the repository root on a Windows machine with the .NET 10 SDK:

- `dotnet restore MinimalMarkdownReader.sln` restores NuGet dependencies.
- `dotnet build MinimalMarkdownReader.sln --configuration Debug` compiles the solution.
- `dotnet run --project src/MinimalMarkdownReader/MinimalMarkdownReader.csproj` launches the app.
- `dotnet test MinimalMarkdownReader.sln` runs automated tests when test projects are present.
- `dotnet publish src/MinimalMarkdownReader/MinimalMarkdownReader.csproj -c Release -r win-x64` creates a Windows x64 publish output; document runtime assumptions, including WebView2, alongside release instructions.

Do not report GUI or Windows-specific validation unless it was actually run on a supported Windows environment. Record manual checks in `docs/TEST_PLAN.md`.

## Coding Style & Naming

Use four spaces for C# indentation and follow standard .NET naming: PascalCase for types and public members, camelCase for locals and parameters, and `_camelCase` for private fields. Name XAML files after their view or window. Keep settings and rendering changes small and validated; treat Markdown as untrusted input, disable or sanitize raw HTML, and constrain local resource loading to the document folder.

## Testing Guidelines

Place automated tests in a clearly named test project (for example, `tests/MinimalMarkdownReader.Tests/`) and use descriptive names such as `OpeningMarkdown_ReplacesCurrentDocument`. Cover parsing, settings validation, unsafe markup and URI handling, and document-loading failures. Include manual WebView2 checks for rendering, links, relative images, and layout in `docs/TEST_PLAN.md`.

## Commits & Pull Requests

This checkout is not a Git repository, so no existing commit convention can be confirmed. Use short, imperative commit subjects (for example, `Add safe relative image loading`). Pull requests should summarize behavior changes, list validation commands and results, link relevant issues, and include screenshots for visible UI changes. Call out unverified Windows-only behavior.

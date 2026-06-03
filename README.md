# PresentAI

PresentAI is a WinUI 3 Windows desktop app that turns a suggested prompt into a presentation deck with speaker notes. It can use either the OpenAI API or the Gemini API, and it includes a speaker console plus a separate presentation window for an external monitor or projector.

## What You Need

- Windows 10 version 1809 or newer, or Windows 11.
- An OpenAI API key or a Gemini API key for AI-generated decks.
- Chrome or Edge if you want to use browser tab casting with Google Cast.
- A second display, projector, AirPlay receiver, Google Cast device, or display-connected browser if you want an audience-facing view.

You can still try the app without an API key. Leave the API key field blank and PresentAI will create a simple local draft deck so you can test the workflow.

## Download And Run

1. Open the latest GitHub release for this repository.
2. Download `PresentAI-win-x64.zip`.
3. Extract the zip file.
4. Run `PresentAI.exe`.

Windows may show a SmartScreen warning for unsigned builds. Choose **More info**, then **Run anyway** if you trust the build.

## Generate A Deck

1. Choose `OpenAI` or `Gemini` from the provider menu.
2. Paste your API key.
3. Keep the default model or enter another supported model name.
   - OpenAI default: `gpt-4.1-mini`
   - Gemini default: `gemini-1.5-flash`
4. Pick the slide count.
5. Enter a suggested prompt, topic, outline, or rough idea.
6. Select **Generate Deck**.

PresentAI creates slide titles, bullets, speaker notes, and visual direction for each slide.

## Import An HTML Deck

Use **Import HTML Deck** to bring an already designed `.html` or `.htm` presentation into PresentAI. The importer reformats the HTML into PresentAI's deck model so it works with the speaker notes panel, slide controls, presentation view, receiver URL, and Markdown export.

Supported import patterns:

- JavaScript decks with a `const slides = [...]`, `let slides = [...]`, or `var slides = [...]` array.
- Slide objects with fields such as `title`, `reference`, `verseText`, `body`, `explanation`, `footer`, and `notes`.
- Basic HTML presentations built from `section`, `article`, or `.slide` blocks.
- Continuous HTML pages that can be split on top-level headings.

For designed HTML files, PresentAI keeps the content and speaker notes, then applies the app's native presentation formatting.

## Present With Speaker View

The main window is the speaker console. It shows:

- Current slide preview.
- Speaker notes.
- Next slide title.
- Timer.
- Previous and next controls.
- Markdown export.

The **Theme** controls let you switch between built-in color schemes and WinUI backdrop modes: Solid, Acrylic, Mica, and Tabbed Mica. Backdrop effects apply to the speaker console while the presentation view keeps a high-contrast audience display.

Keyboard controls:

- `Right Arrow`, `Page Down`, or `Space`: next slide.
- `Left Arrow`, `Page Up`, or `Backspace`: previous slide.

## Show The Presentation View

1. Connect an external display or projector.
2. Pick the target display in the **Display** menu.
3. Select **Open Presentation View**.
4. Use **Full Screen / Restore** if needed.

The presentation view is a separate audience-facing window. Keep the speaker console on your laptop screen and move the presentation view to the projector or external display.

## Use AirPlay Or Google Cast

PresentAI hosts a live receiver while the app is running. The app shows both a local URL and a network URL.

Options:

- Use the `Local` URL, usually `http://127.0.0.1:...`, when opening the receiver on the same computer.
- Use the `Network` URL when opening the receiver from another device on the same network.
- Select **Open Receiver URL** to open the local live browser view.
- Select **Open Chrome/Edge Cast Window** to open the receiver in Chrome or Edge with Chromium media routing enabled.
- Use Chrome or Edge's built-in cast menu to cast the receiver tab to a Google Cast device.
- Use Windows display projection, AirPlay mirroring software, or a display-connected browser to show the receiver URL on another screen.

The native presentation window is still the most reliable option for wired displays and projectors.

## Export Notes

Select **Export Markdown** to save the generated slides and speaker notes as a Markdown file.

## Build From Source

Install the .NET 10 SDK and the Windows App SDK tooling, then run:

```powershell
dotnet restore
dotnet build
dotnet run
```

To publish a self-contained Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:WindowsAppSDKSelfContained=true
```

The executable is created in:

```text
bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\PresentAI.exe
```

## GitHub Actions Releases

This repository includes a GitHub Actions workflow at `.github/workflows/release.yml`.

The workflow:

- Runs on Windows.
- Installs the .NET 10 SDK.
- Restores the project.
- Publishes a self-contained `win-x64` executable.
- Compresses the published output into `PresentAI-win-x64.zip`.
- Uploads the zip as a workflow artifact.
- Attaches the zip to a GitHub Release when you push a version tag.

To create a release:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will create or update a release named `v1.0.0` and attach `PresentAI-win-x64.zip`.

You can also run the workflow manually from the **Actions** tab. Manual runs upload the zip as a workflow artifact but do not create a GitHub Release unless the run is for a tag.

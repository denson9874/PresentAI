# PresentAI

PresentAI is a WinUI 3 Windows desktop app for generating presentation decks and speaker notes from a prompt using the OpenAI API or Gemini API.

## Features

- Generate a structured slide deck with speaker notes from a suggested prompt.
- Use OpenAI or Gemini by pasting an API key and choosing a model.
- Leave the API key blank to create a local draft deck for testing the UI.
- Speaker view with slide preview, notes, next-slide preview, timer, and navigation controls.
- Presentation view that can be moved full-screen to an external monitor or projector.
- Local browser receiver URL for Google Cast, AirPlay mirroring workflows, display-connected browsers, and browser tab casting.
- Export the generated deck and notes to Markdown.

## Run

```powershell
dotnet run
```

If you do not have the WinUI 3 templates or Windows App SDK packages installed, install the Windows App SDK tooling for your .NET SDK.

## Casting Notes

The most reliable presentation path is the native presentation window on an external display. For AirPlay and Google Cast, PresentAI hosts a live local receiver URL. Open that URL in Chrome, Edge, or a device-connected browser, then use the browser or operating system's casting/mirroring controls. The `Open Chrome/Edge Cast Window` button launches the receiver in a browser with Chromium media routing enabled when Chrome or Edge is installed.

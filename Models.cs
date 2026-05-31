using System.Text.Json.Serialization;

namespace PresentAI;

public sealed class Deck
{
    public string Title { get; set; } = "Untitled Presentation";
    public string Subtitle { get; set; } = "";
    public List<Slide> Slides { get; set; } = [];
}

public sealed class Slide
{
    public string Title { get; set; } = "";
    public List<string> Bullets { get; set; } = [];
    public string Notes { get; set; } = "";
    public string VisualHint { get; set; } = "";
}

public sealed class DeckGenerationRequest
{
    public string Provider { get; init; } = "OpenAI";
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "";
    public string Prompt { get; init; } = "";
    public int SlideCount { get; init; } = 8;
}

public sealed class PresentationState
{
    [JsonPropertyName("deck")]
    public Deck Deck { get; init; } = new();

    [JsonPropertyName("slideIndex")]
    public int SlideIndex { get; init; }
}

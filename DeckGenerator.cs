using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PresentAI;

public sealed class DeckGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _http = new();

    public async Task<Deck> GenerateAsync(DeckGenerationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Enter a presentation prompt first.");
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BuildLocalDraft(request.Prompt, request.SlideCount);
        }

        var systemPrompt =
            "Create a concise business-ready presentation deck. Return only valid JSON with this shape: " +
            "{\"title\":\"string\",\"subtitle\":\"string\",\"slides\":[{\"title\":\"string\",\"bullets\":[\"string\"],\"notes\":\"speaker notes paragraph\",\"visualHint\":\"short visual direction\"}]}. " +
            "Each slide needs 3 to 5 bullets and useful speaker notes.";

        var userPrompt = $"Topic or suggested prompt: {request.Prompt}\nSlide count: {request.SlideCount}";
        var json = request.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            ? await GenerateWithGeminiAsync(request, systemPrompt, userPrompt, cancellationToken)
            : await GenerateWithOpenAiAsync(request, systemPrompt, userPrompt, cancellationToken);

        var deck = ParseDeck(json);
        return deck.Slides.Count > 0 ? deck : BuildLocalDraft(request.Prompt, request.SlideCount);
    }

    private async Task<string> GenerateWithOpenAiAsync(DeckGenerationRequest request, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? "gpt-4.1-mini" : request.Model.Trim();
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());

        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" },
            temperature = 0.7
        };

        message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(responseText);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<string> GenerateWithGeminiAsync(DeckGenerationRequest request, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? "gemini-1.5-flash" : request.Model.Trim();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(request.ApiKey.Trim())}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = $"{systemPrompt}\n\n{userPrompt}" }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                responseMimeType = "application/json"
            }
        };

        using var response = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(responseText);
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
    }

    private static Deck ParseDeck(string rawJson)
    {
        var cleaned = rawJson.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            cleaned = firstNewLine >= 0 && lastFence > firstNewLine
                ? cleaned[(firstNewLine + 1)..lastFence].Trim()
                : cleaned.Trim('`');
        }

        var deck = JsonSerializer.Deserialize<Deck>(cleaned, JsonOptions) ?? new Deck();
        deck.Title = string.IsNullOrWhiteSpace(deck.Title) ? "Generated Presentation" : deck.Title.Trim();
        deck.Slides = deck.Slides
            .Where(s => !string.IsNullOrWhiteSpace(s.Title) || s.Bullets.Count > 0)
            .Take(20)
            .ToList();

        foreach (var slide in deck.Slides)
        {
            slide.Title = slide.Title.Trim();
            slide.Bullets = slide.Bullets.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b.Trim()).Take(6).ToList();
            slide.Notes = slide.Notes.Trim();
            slide.VisualHint = slide.VisualHint.Trim();
        }

        return deck;
    }

    private static Deck BuildLocalDraft(string prompt, int slideCount)
    {
        var count = Math.Clamp(slideCount, 4, 12);
        var deck = new Deck
        {
            Title = prompt.Length > 64 ? prompt[..64].Trim() + "..." : prompt.Trim(),
            Subtitle = "Local draft generated without an API key"
        };

        deck.Slides.Add(new Slide
        {
            Title = "Opening",
            Bullets = ["Frame the topic", "Name the desired outcome", "Preview the story"],
            Notes = $"Introduce the presentation topic: {prompt}. Give the audience a simple reason to care before moving into the main sections.",
            VisualHint = "Strong title slide with one clear image or large typographic statement"
        });

        for (var i = 2; i < count; i++)
        {
            deck.Slides.Add(new Slide
            {
                Title = $"Key Point {i - 1}",
                Bullets = ["Main idea", "Supporting evidence", "Practical implication"],
                Notes = "Use this slide to develop one specific idea. Keep the spoken explanation more detailed than the on-screen bullets.",
                VisualHint = "Simple diagram, quote, metric, or comparison"
            });
        }

        deck.Slides.Add(new Slide
        {
            Title = "Close",
            Bullets = ["Summarize the takeaway", "Invite questions", "End with next steps"],
            Notes = "Bring the story back to the original prompt and leave the audience with a clear action or decision.",
            VisualHint = "Clean closing slide with one memorable phrase"
        });

        return deck;
    }
}

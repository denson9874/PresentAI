using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PresentAI;

public static class HtmlDeckImporter
{
    public static Deck Import(string html, string sourceName)
    {
        var deck = ImportScriptSlides(html, sourceName);
        return deck.Slides.Count > 0 ? deck : ImportSectionSlides(html, sourceName);
    }

    private static Deck ImportScriptSlides(string html, string sourceName)
    {
        var arrayText = ExtractSlidesArray(html);
        if (string.IsNullOrWhiteSpace(arrayText))
        {
            return new Deck();
        }

        var objects = SplitTopLevelObjects(arrayText);
        var slides = objects.Select(ParseSlideObject).Where(slide => slide is not null).Cast<Slide>().ToList();
        if (slides.Count == 0)
        {
            return new Deck();
        }

        var title = slides.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Title))?.Title ?? TitleFromHtml(html, sourceName);
        return new Deck
        {
            Title = CleanLine(title),
            Subtitle = $"Imported from {sourceName}",
            Slides = slides
        };
    }

    private static string ExtractSlidesArray(string html)
    {
        var marker = Regex.Match(html, @"\b(?:const|let|var)\s+slides\s*=", RegexOptions.IgnoreCase);
        if (!marker.Success)
        {
            return "";
        }

        var start = html.IndexOf('[', marker.Index + marker.Length);
        if (start < 0)
        {
            return "";
        }

        var depth = 0;
        var quote = '\0';
        var inTemplate = false;
        var escaped = false;

        for (var i = start; i < html.Length; i++)
        {
            var ch = html[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (quote != '\0')
            {
                if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == quote)
                {
                    quote = '\0';
                    inTemplate = false;
                }
                continue;
            }

            if (ch is '"' or '\'' or '`')
            {
                quote = ch;
                inTemplate = ch == '`';
                continue;
            }

            if (!inTemplate && ch == '[')
            {
                depth++;
            }
            else if (!inTemplate && ch == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return html[start..(i + 1)];
                }
            }
        }

        return "";
    }

    private static List<string> SplitTopLevelObjects(string arrayText)
    {
        var objects = new List<string>();
        var depth = 0;
        var objectStart = -1;
        var quote = '\0';
        var escaped = false;

        for (var i = 0; i < arrayText.Length; i++)
        {
            var ch = arrayText[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (quote != '\0')
            {
                if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (ch is '"' or '\'' or '`')
            {
                quote = ch;
                continue;
            }

            if (ch == '{')
            {
                if (depth == 0)
                {
                    objectStart = i;
                }
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0 && objectStart >= 0)
                {
                    objects.Add(arrayText[objectStart..(i + 1)]);
                    objectStart = -1;
                }
            }
        }

        return objects;
    }

    private static Slide? ParseSlideObject(string objectText)
    {
        var values = ParseProperties(objectText);
        if (values.Count == 0)
        {
            return null;
        }

        var title = FirstValue(values, "title", "reference", "heading", "number");
        var body = FirstValue(values, "body", "verseText", "text", "content", "explanation");
        var explanation = FirstValue(values, "explanation", "subtitle", "footer");
        var footer = FirstValue(values, "footer", "version");
        var notes = FirstValue(values, "notes", "speakerNotes", "speaker_notes");

        var bullets = new List<string>();
        AddTextAsBullets(bullets, body);
        AddTextAsBullets(bullets, explanation);
        AddTextAsBullets(bullets, footer);

        if (string.IsNullOrWhiteSpace(title) && bullets.Count > 0)
        {
            title = bullets[0];
            bullets.RemoveAt(0);
        }

        if (string.IsNullOrWhiteSpace(title) && bullets.Count == 0)
        {
            return null;
        }

        return new Slide
        {
            Title = CleanLine(title),
            Bullets = bullets.Select(CleanLine).Where(line => line.Length > 0).Distinct().Take(5).ToList(),
            Notes = CleanNotes(notes),
            VisualHint = FirstValue(values, "theme", "bgClass")
        };
    }

    private static Dictionary<string, string> ParseProperties(string objectText)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < objectText.Length; i++)
        {
            while (i < objectText.Length && !IsIdentifierStart(objectText[i]))
            {
                i++;
            }

            if (i >= objectText.Length)
            {
                break;
            }

            var keyStart = i;
            while (i < objectText.Length && IsIdentifierPart(objectText[i]))
            {
                i++;
            }

            var key = objectText[keyStart..i];
            while (i < objectText.Length && char.IsWhiteSpace(objectText[i]))
            {
                i++;
            }

            if (i >= objectText.Length || objectText[i] != ':')
            {
                continue;
            }

            i++;
            while (i < objectText.Length && char.IsWhiteSpace(objectText[i]))
            {
                i++;
            }

            if (i >= objectText.Length || objectText[i] is not ('"' or '\'' or '`'))
            {
                continue;
            }

            var quote = objectText[i++];
            var value = ReadQuotedValue(objectText, ref i, quote);
            values[key] = DecodeJavaScriptString(value);
        }

        return values;
    }

    private static string ReadQuotedValue(string text, ref int index, char quote)
    {
        var builder = new StringBuilder();
        var escaped = false;

        for (; index < text.Length; index++)
        {
            var ch = text[index];
            if (escaped)
            {
                builder.Append('\\');
                builder.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == quote)
            {
                return builder.ToString();
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static Deck ImportSectionSlides(string html, string sourceName)
    {
        var body = Regex.Replace(html, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        var matches = Regex.Matches(body, @"<(section|article|div)\b[^>]*(?:class\s*=\s*[""'][^""']*\bslide\b[^""']*[""'][^>]*)?>([\s\S]*?)</\1>", RegexOptions.IgnoreCase);
        var candidates = matches
            .Select(match => match.Groups[2].Value)
            .Where(fragment => Regex.IsMatch(fragment, @"<h[1-6]\b|<li\b|<p\b", RegexOptions.IgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = Regex.Split(body, @"(?=<h[1-2]\b)", RegexOptions.IgnoreCase)
                .Where(fragment => Regex.IsMatch(fragment, @"<h[1-6]\b", RegexOptions.IgnoreCase))
                .ToList();
        }

        var slides = candidates.Select(ParseHtmlFragment).Where(slide => slide is not null).Cast<Slide>().ToList();
        return new Deck
        {
            Title = TitleFromHtml(html, sourceName),
            Subtitle = $"Imported from {sourceName}",
            Slides = slides
        };
    }

    private static Slide? ParseHtmlFragment(string fragment)
    {
        var heading = Regex.Match(fragment, @"<h[1-6]\b[^>]*>([\s\S]*?)</h[1-6]>", RegexOptions.IgnoreCase);
        var title = heading.Success ? HtmlToText(heading.Groups[1].Value) : "";

        var bullets = Regex.Matches(fragment, @"<li\b[^>]*>([\s\S]*?)</li>", RegexOptions.IgnoreCase)
            .Select(match => HtmlToText(match.Groups[1].Value))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        if (bullets.Count == 0)
        {
            bullets = Regex.Matches(fragment, @"<p\b[^>]*>([\s\S]*?)</p>", RegexOptions.IgnoreCase)
                .Select(match => HtmlToText(match.Groups[1].Value))
                .Where(text => !string.IsNullOrWhiteSpace(text) && !text.Equals(title, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(title) && bullets.Count > 0)
        {
            title = bullets[0];
            bullets.RemoveAt(0);
        }

        if (string.IsNullOrWhiteSpace(title) && bullets.Count == 0)
        {
            return null;
        }

        var notesMatch = Regex.Match(fragment, @"<(aside|div|section)\b[^>]*(class|id)\s*=\s*[""'][^""']*(notes?|speaker)[^""']*[""'][^>]*>([\s\S]*?)</\1>", RegexOptions.IgnoreCase);

        return new Slide
        {
            Title = CleanLine(title),
            Bullets = bullets.Select(CleanLine).Where(line => line.Length > 0).Take(5).ToList(),
            Notes = notesMatch.Success ? CleanNotes(HtmlToText(notesMatch.Groups[4].Value)) : "",
            VisualHint = "Imported HTML slide"
        };
    }

    private static void AddTextAsBullets(List<string> bullets, string value)
    {
        foreach (var line in value.Replace("\\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                bullets.Add(line);
            }
        }
    }

    private static string FirstValue(Dictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string TitleFromHtml(string html, string sourceName)
    {
        var title = Regex.Match(html, @"<title\b[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase);
        return title.Success ? CleanLine(HtmlToText(title.Groups[1].Value)) : Path.GetFileNameWithoutExtension(sourceName);
    }

    private static string HtmlToText(string html)
    {
        var withBreaks = Regex.Replace(html, @"<\s*br\s*/?\s*>|</\s*(p|div|li|h[1-6])\s*>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withBreaks, "<[^>]+>", " ");
        return CleanNotes(WebUtility.HtmlDecode(withoutTags));
    }

    private static string DecodeJavaScriptString(string value)
    {
        return value
            .Replace("\\r\\n", "\n")
            .Replace("\\n", "\n")
            .Replace("\\r", "\n")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace("\\`", "`")
            .Replace("\\\\", "\\");
    }

    private static string CleanLine(string value)
    {
        return Regex.Replace(CleanNotes(value), @"\s+", " ").Trim();
    }

    private static string CleanNotes(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"\*\*(.*?)\*\*", "$1");
        normalized = Regex.Replace(normalized, @"\*(.*?)\*", "$1");
        normalized = Regex.Replace(normalized, @"[ \t]+\n", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static bool IsIdentifierStart(char ch) => char.IsLetter(ch) || ch == '_' || ch == '$';
    private static bool IsIdentifierPart(char ch) => char.IsLetterOrDigit(ch) || ch == '_' || ch == '$';
}

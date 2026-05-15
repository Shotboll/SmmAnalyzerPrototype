using System.Text;

namespace SmmAnalyzerPrototype.Api.Services
{
    public static class LlmJsonExtractor
    {
        public static string ExtractJson(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "[]";

            response = response.Trim();

            var fencedJson = ExtractFromCodeFence(response, "json");
            if (!string.IsNullOrWhiteSpace(fencedJson))
                return fencedJson;

            var fencedCode = ExtractFromCodeFence(response, null);
            if (!string.IsNullOrWhiteSpace(fencedCode) && LooksLikeJson(fencedCode))
                return fencedCode;

            var embeddedJson = ExtractFirstJsonObjectOrArray(response);
            if (!string.IsNullOrWhiteSpace(embeddedJson))
                return embeddedJson;

            if (LooksLikeJson(response))
                return response;

            return "[]";
        }

        public static string ExtractJsonGrammar(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "[]";

            response = response.Trim();

            var fencedJson = ExtractFromCodeFence(response, "json");
            if (!string.IsNullOrWhiteSpace(fencedJson))
                return fencedJson;

            var fencedCode = ExtractFromCodeFence(response, null);
            if (!string.IsNullOrWhiteSpace(fencedCode))
                return fencedCode;

            return response;
        }

        private static string? ExtractFromCodeFence(string text, string? language)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string openingFence = language == null ? "```" : $"```{language}";
            int start = text.IndexOf(openingFence, StringComparison.OrdinalIgnoreCase);

            if (start < 0)
                return null;

            start += openingFence.Length;

            while (start < text.Length && (text[start] == '\r' || text[start] == '\n'))
                start++;

            int end = text.IndexOf("```", start, StringComparison.OrdinalIgnoreCase);

            if (end <= start)
                return null;

            return text.Substring(start, end - start).Trim();
        }

        private static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            return (text.StartsWith("{") && text.EndsWith("}")) ||
                   (text.StartsWith("[") && text.EndsWith("]"));
        }

        private static string? ExtractFirstJsonObjectOrArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            int objectStart = text.IndexOf('{');
            int arrayStart = text.IndexOf('[');

            int start;
            char openChar;
            char closeChar;

            if (objectStart == -1 && arrayStart == -1)
                return null;

            if (objectStart == -1 || (arrayStart != -1 && arrayStart < objectStart))
            {
                start = arrayStart;
                openChar = '[';
                closeChar = ']';
            }
            else
            {
                start = objectStart;
                openChar = '{';
                closeChar = '}';
            }

            var sb = new StringBuilder();
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                sb.Append(c);

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == openChar)
                {
                    depth++;
                }
                else if (c == closeChar)
                {
                    depth--;

                    if (depth == 0)
                        return sb.ToString().Trim();
                }
            }

            return null;
        }
    }
}
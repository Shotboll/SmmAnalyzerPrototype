using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmmAnalyzerPrototype.Api.Services
{
    public class LanguageToolService
    {
        private readonly HttpClient _httpClient;

        public LanguageToolService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<GrammarErrorDto>> CheckTextAsync(string text, string language = "ru")
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("language", language),
                new KeyValuePair<string, string>("enabledOnly", "false")
            });

            var response = await _httpClient.PostAsync("http://localhost:8010/v2/check", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LanguageToolResponse>(json);

            if (result?.Matches == null)
                return new List<GrammarErrorDto>();

            var errors = new List<GrammarErrorDto>();

            foreach (var match in result.Matches)
            {
                if (match.Offset < 0 || match.Length <= 0 || match.Offset + match.Length > text.Length)
                    continue;

                var fragment = text.Substring(match.Offset, match.Length);

                errors.Add(new GrammarErrorDto
                {
                    Fragment = fragment,
                    Suggestion = match.Replacements?.FirstOrDefault()?.Value ?? string.Empty,
                    Type = match.Rule?.IssueType ?? "grammar",
                    Offset = match.Offset,
                    Length = match.Length,
                    Message = match.Message ?? string.Empty
                });
            }

            return errors;
        }

        private class LanguageToolResponse
        {
            [JsonPropertyName("matches")]
            public List<Match> Matches { get; set; } = new();
        }

        private class Match
        {
            [JsonPropertyName("offset")]
            public int Offset { get; set; }

            [JsonPropertyName("length")]
            public int Length { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("replacements")]
            public List<Replacement>? Replacements { get; set; }

            [JsonPropertyName("rule")]
            public Rule? Rule { get; set; }
        }

        private class Replacement
        {
            [JsonPropertyName("value")]
            public string Value { get; set; } = string.Empty;
        }

        private class Rule
        {
            [JsonPropertyName("issueType")]
            public string? IssueType { get; set; }
        }
    }
}
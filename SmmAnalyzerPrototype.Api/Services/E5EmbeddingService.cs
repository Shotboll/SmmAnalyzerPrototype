using System.Text.Json.Serialization;

namespace SmmAnalyzerPrototype.Api.Services
{
    public class E5EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;

        public E5EmbeddingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<float[]> GetEmbeddingAsync(string text, bool isQuery = false)
        {
            var task = isQuery ? "query" : "passage";
            var url = $"embed?text={Uri.EscapeDataString(text)}&task={task}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
            return json.Embedding;
        }
        private class EmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; }
        }
    }
}

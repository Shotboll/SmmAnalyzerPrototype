using AspNetCoreGeneratedDocument;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;
using RAGTEST.Data;
using System.Text;
using System.Text.Json;

namespace RAGTEST.Services
{
    public class LlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly IEmbeddingService _embeddingService;
        private readonly AppDbContext _context;

        public LlmService(HttpClient httpClient, IConfiguration configuration, IEmbeddingService embeddingService, AppDbContext context)
        {
            _embeddingService = embeddingService;
            _context = context;
            _httpClient = httpClient;
            _model = configuration["Llm:Model"] ?? "saiga_yandexgpt_8b";
        }
        private float[] NormalizeVector(float[] vector)
        {
            float norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (norm == 0) return vector;
            return vector.Select(x => x / norm).ToArray();
        }

        public async Task<string> AnalyzePostWithRagAsync(string postText, Guid communityId, int topK = 5)
        {
            float[] queryEmbedding = await _embeddingService.GetEmbeddingAsync(postText, isQuery: true);
            float[] normalizedQuery = NormalizeVector(queryEmbedding);

            var allChunks = await _context.RegulationChunks
            .Where(c => c.CommunityId == communityId)
            .ToListAsync();

            var chunks = await _context.RegulationChunks
                .FromSqlRaw(@"
                    SELECT * FROM regulation_chunks
                    WHERE community_id = {0}
                    ORDER BY embedding <=> {1}::vector
                    LIMIT {2}
                ", communityId, normalizedQuery, topK)
                .ToListAsync();

            var contextText = string.Join("\n\n", chunks.Select((x, i) =>
                $"Правило {i + 1} (категория: {x.MetadataJson}):\n{x.ChunkText}"
            ));

            var prompt = $@"
                Ты — модератор контента. Ниже приведены правила сообщества.

                {contextText}

                Пост: {postText}

                Определи, нарушает ли пост эти правила. Отвечай ТОЛЬКО в формате JSON:

                {{
                  ""hasViolations"": true/false,
                  ""violations"": [
                    {{
                      ""ruleNumber"": <номер правила>,
                      ""explanation"": ""<почему нарушает>""
                    }}
                  ],
                  ""comment"": ""<общий комментарий>""
                }}

                Важно: если пост не нарушает никаких правил, верни {{ ""hasViolations"": false, ""violations"": [] }}.
                Не придумывай нарушения там, где их нет. Правила применяй буквально. 
                ";

            var requestBody = new
            {
                model = _model,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);
            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);
            return doc.RootElement.GetProperty("response").GetString() ?? "Ошибка получения ответа";
        }
    }
}

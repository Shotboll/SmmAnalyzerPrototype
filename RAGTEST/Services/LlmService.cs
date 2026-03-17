using AspNetCoreGeneratedDocument;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;
using RAGTEST.Data;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

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
            _model = configuration["Llm:Model"] ?? "saiga_nemo_12b";
        }

        public async Task<string> CallLlmAsync(string prompt)
        {
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
            return doc.RootElement.GetProperty("response").GetString() ?? "";
        }

        private float[] NormalizeVector(float[] vector)
        {
            float norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (norm == 0) return vector;
            return vector.Select(x => x / norm).ToArray();
        }

        public async Task<string> AnalyzePostWithRagAsync(string postText, Guid regulationId, int topK = 5)
        {
            float[] queryEmbedding = await _embeddingService.GetEmbeddingAsync(postText, isQuery: true);
            float[] normalizedQuery = NormalizeVector(queryEmbedding);

            var allChunks = await _context.RegulationChunks
            .Where(c => c.RegulationId == regulationId)
            .ToListAsync();

            var chunks = await _context.RegulationChunks
                .FromSqlRaw(@"
                    SELECT * FROM regulation_chunks
                    WHERE regulation_id = {0}
                    ORDER BY embedding <=> {1}::vector
                    LIMIT {2}
                ", regulationId, normalizedQuery, topK)
                .ToListAsync();

            var contextText = string.Join("\n\n", chunks.Select((x, i) =>
                $"Правило {i + 1} (\n{x.ChunkText}"
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
                      ""ruleNumber"": <номер правила>, <содержание правила>
                      ""explanation"": ""<почему нарушает>""
                    }}
                  ],
                  ""comment"": ""<общий комментарий>""
                }}

                Важно: если пост не нарушает никаких правил, верни {{ ""hasViolations"": false, ""violations"": [] }}.
                Не придумывай нарушения там, где их нет. Правила применяй буквально. 
                ";

            return await CallLlmAsync(prompt);
        }

        public async Task<string> CheckErrorText(string postText, Guid communityId)
        {
            var prompt = $@"
                Ты — корректор текстов. Проверяй ТОЛЬКО реальный текст ниже.

                ПРАВИЛА:
                1. Находи ТОЛЬКО явные орфографические и пунктуационные ошибки
                2. Если слово написано правильно — НЕ отмечай его
                3. Если сомневаешься — считай, что ошибки нет
                4. НЕ выдумывай ошибки
                5. НЕ исправляй стилистику, только орфографию и пунктуацию

                ФОРМАТ ОТВЕТА (ТОЛЬКО JSON):
                [
                  {{
                    ""fragment"": ""ошибочный фрагмент"",
                    ""suggestion"": ""исправленный вариант"",
                    ""type"": ""орфография"" или ""пунктуация""
                  }}
                ]

                Если ошибок нет — верни пустой массив: []

                ТЕКСТ ДЛЯ ПРОВЕРКИ:
                {postText}
            ";

            return await CallLlmAsync(prompt);
        }

        public async Task<string> StyleCheck(string audience, string style, string text)
        {
            var prompt = $@"
                    Ты — стилист текстов для социальных сетей. Оцени, соответствует ли данный пост стилю и целевой аудитории. Не обращай внимания на Орфографию и пунктуацию

                    Целевая аудитория: {audience}
                    Желаемый стиль: {style}

                    Текст поста: ""{text}""

                    Дай развёрнутый ответ в свободной форме:
                    - Соответствует ли стиль аудитории? (да/нет/частично)
                    - Если нет или частично, что именно не так?
                    - Как можно улучшить текст, чтобы он лучше подходил?
                ";

            return await CallLlmAsync(prompt);
        }
    }
}

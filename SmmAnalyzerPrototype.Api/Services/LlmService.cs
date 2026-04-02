using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmmAnalyzerPrototype.Api.Services
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

        public enum LlmMode { Strict, Creative, RAG }

        public async Task<string> CallLlmAsync(string prompt, LlmMode mode)
        {
            var options = mode switch
            {
                LlmMode.Strict => new { temperature = 0.1, top_p = 0.1, num_predict = 500, num_ctx = 4096 },
                LlmMode.RAG => new { temperature = 0.1, top_p = 0.2, num_predict = 500, num_ctx = 4096 },
                LlmMode.Creative => new { temperature = 0.8, top_p = 0.9, num_predict = 700, num_ctx = 4096 },
                _ => new { temperature = 0.0, top_p = 0.1, num_predict = 500, num_ctx = 4096 }
            };

            var requestBody = new
            {
                model = _model,
                prompt = prompt,
                stream = false,
                options = options
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(result);

                // Извлекаем ответ и обрезаем лишние пробелы
                return doc.RootElement.GetProperty("response").GetString()?.Trim() ?? "";
            }
            catch (Exception ex)
            {
                // Для диплома важно логировать ошибки локального сервера
                Console.WriteLine($"Ошибка при обращении к Ollama: {ex.Message}");
                return string.Empty;
            }
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

            var chunks = await _context.RegulationChunks
                .FromSqlRaw(@"
                    SELECT c.*
                    FROM regulation_chunks c
                    INNER JOIN regulation_documents d ON c.""RegulationId"" = d.""Id""
                    WHERE d.""CommunityId"" = {0}
                    ORDER BY c.embedding <=> {1}::vector
                    LIMIT {2}
                ", communityId, normalizedQuery, topK)
                .ToListAsync(); ;

            var contextText = string.Join("\n\n", chunks.Select((x, i) =>
                $"Правило {i + 1} (\n{x.ChunkText}"
            ));

            var prompt = $@"
                Ты — модератор контента. Ниже приведены правила сообщества:

                {contextText}

                Пост:
                {postText}

                Определи, нарушает ли пост эти правила. Отвечай ТОЛЬКО в формате JSON:

                {{
                  ""hasViolations"": true/false,
                  ""violations"": [
                    {{
                      ""ruleNumber"": <номер правила>,
                      ""ruleText"": ""<текст правила>"",
                      ""explanation"": ""<почему нарушает>""
                    }}
                  ],
                  ""comment"": ""<общий комментарий или 'Нет нарушений'>""
                }}

                ВАЖНО:
                1. Если пост не нарушает правил, верни:
                   {{
                     ""hasViolations"": false,
                     ""violations"": [],
                     ""comment"": ""Нет нарушений""
                   }}
                2. Не придумывай нарушения там, где их нет.
                3. Применяй правила строго, буквально.
                4. Ответ должен быть валидным JSON, без лишнего текста.
                ";

            string response = await CallLlmAsync(prompt, LlmMode.RAG);

            // Удаляем возможную markdown-обёртку
            var jsonStart = response.IndexOf("```json");
            if (jsonStart != -1)
            {
                jsonStart += 7; // длина "```json"
                var jsonEnd = response.IndexOf("```", jsonStart);
                if (jsonEnd != -1)
                {
                    response = response.Substring(jsonStart, jsonEnd - jsonStart).Trim();
                }
            }
            else
            {
                // Если нет обёртки, но есть просто "```", тоже удаляем
                var simpleStart = response.IndexOf("```");
                if (simpleStart != -1)
                {
                    simpleStart += 3;
                    var simpleEnd = response.IndexOf("```", simpleStart);
                    if (simpleEnd != -1)
                    {
                        response = response.Substring(simpleStart, simpleEnd - simpleStart).Trim();
                    }
                }
            }

            return response;
        }

        public async Task<List<GrammarExplanationDto>> ExplainGrammarErrorsAsync(string originalText, List<GrammarErrorDto> rawErrors)
        {
            if (string.IsNullOrWhiteSpace(originalText) || rawErrors == null || rawErrors.Count == 0)
                return new List<GrammarExplanationDto>();

            var promptObject = new
            {
                text = originalText,
                errors = rawErrors.Select((e, index) => new
                {
                    index = index,
                    fragment = e.Fragment,
                    suggestion = e.Suggestion,
                    type = e.Type,
                    offset = e.Offset,
                    length = e.Length,
                    message = e.Message
                }).ToList()
            };

            var promptJson = JsonSerializer.Serialize(promptObject, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var prompt = $$"""
                Ты — редактор русского текста.
                Тебе уже передан список найденных ошибок. Не ищи новые ошибки и не пропускай существующие.

                Ответь строго валидным JSON-массивом такого вида:
                [
                  {
                    "index": 0,
                    "explanation": "Краткое объяснение ошибки",
                    "hint": "Совет по исправлению ошибки"
                  }
                ]

                Текст и ошибки:
                {{promptJson}}
                """;

            var rawResponse = await CallLlmAsync(prompt, LlmMode.Strict);
            rawResponse = ExtractJson(rawResponse);

            try
            {
                var result = JsonSerializer.Deserialize<List<GrammarExplanationDto>>(rawResponse);
                return result ?? new List<GrammarExplanationDto>();
            }
            catch
            {
                return rawErrors.Select((e, i) => new GrammarExplanationDto
                {
                    Index = i,
                    Explanation = $"Ошибка в фрагменте \"{e.Fragment}\".",
                    Hint = !string.IsNullOrWhiteSpace(e.Message) ? e.Message : "Проверьте предложенное исправление."
                }).ToList();
            }
        }

        private static string ExtractJson(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "[]";

            var jsonBlockStart = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonBlockStart >= 0)
            {
                jsonBlockStart += 7;
                var jsonBlockEnd = response.IndexOf("```", jsonBlockStart, StringComparison.OrdinalIgnoreCase);
                if (jsonBlockEnd > jsonBlockStart)
                    return response.Substring(jsonBlockStart, jsonBlockEnd - jsonBlockStart).Trim();
            }

            var codeBlockStart = response.IndexOf("```", StringComparison.OrdinalIgnoreCase);
            if (codeBlockStart >= 0)
            {
                codeBlockStart += 3;
                var codeBlockEnd = response.IndexOf("```", codeBlockStart, StringComparison.OrdinalIgnoreCase);
                if (codeBlockEnd > codeBlockStart)
                    return response.Substring(codeBlockStart, codeBlockEnd - codeBlockStart).Trim();
            }

            return response.Trim();
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

                    Не пиши пример готового текста. Хватит только рекомендаций.
                ";

            return await CallLlmAsync(prompt, LlmMode.Strict);
        }
    }
}

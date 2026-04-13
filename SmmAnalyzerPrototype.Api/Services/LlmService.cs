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
                LlmMode.RAG => new { temperature = 0.0, top_p = 0.2, num_predict = 1500, num_ctx = 4096 },
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
                .ToListAsync();

            var contextText = string.Join("\n\n", chunks.Select((x, i) =>
                $"[Правило {i + 1}]\n{x.ChunkText}"
            ));

            var prompt = $@"
                Ты — модератор контента сообщества.

                Ниже приведены правила сообщества:
                {contextText}

                Пост для проверки:
                {postText}

                Определи, нарушает ли пост правила.

                Верни ответ ТОЛЬКО в формате JSON:

                {{
                  ""hasViolations"": true,
                  ""violations"": [
                    {{
                      ""ruleNumber"": 1,
                      ""ruleShort"": ""<краткое название правила>"",
                      ""matchedText"": ""фрагмент поста, который нарушает правило"",
                      ""explanation"": ""краткое объяснение причины нарушения""
                    }}
                  ],
                  ""comment"": ""краткий общий вывод""
                }}

                Если нарушений нет, верни строго:
                {{
                  ""hasViolations"": false,
                  ""violations"": [],
                  ""comment"": ""Нарушений не найдено""
                }}

                Требования:
                1. Не придумывай нарушения, если их нет.
                2. Указывай только те правила, которые действительно относятся к посту.
                3. Для каждого нарушения обязательно укажи matchedText — короткий фрагмент из поста.
                4. explanation должен быть коротким, деловым и понятным.
                5. Если одно и то же нарушение уже описано, не дублируй его.
                6. Ответ должен быть валидным JSON без markdown, без пояснений и без лишнего текста.
                ";

            string response = await CallLlmAsync(prompt, LlmMode.RAG);

            response = ExtractJson(response);

            return response;
        }

        private static string ExtractJson(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "[]";

            response = response.Trim();

            // 1. Сначала пробуем извлечь JSON из markdown-блока ```json ... ```
            var fencedJson = ExtractFromCodeFence(response, "json");
            if (!string.IsNullOrWhiteSpace(fencedJson))
                return fencedJson;

            // 2. Потом пробуем извлечь из обычного блока ``` ... ```
            var fencedCode = ExtractFromCodeFence(response, null);
            if (!string.IsNullOrWhiteSpace(fencedCode) && LooksLikeJson(fencedCode))
                return fencedCode;

            // 3. Если модель добавила пояснения вроде "Формат ответа:"
            //    ищем первый полноценный JSON-объект или массив в тексте
            var embeddedJson = ExtractFirstJsonObjectOrArray(response);
            if (!string.IsNullOrWhiteSpace(embeddedJson))
                return embeddedJson;

            // 4. Если вдруг весь ответ уже похож на JSON
            if (LooksLikeJson(response))
                return response;

            // 5. Безопасный fallback
            return "[]";
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

            // пропускаем возможный перевод строки после ```json
            while (start < text.Length && (text[start] == '\r' || text[start] == '\n'))
                start++;

            int end = text.IndexOf("```", start, StringComparison.OrdinalIgnoreCase);
            if (end <= start)
                return null;

            var content = text.Substring(start, end - start).Trim();
            return content;
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
            rawResponse = ExtractJsonGrammar(rawResponse);

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

        private static string ExtractJsonGrammar(string response)
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

        public async Task<StyleCheckResultDto> StyleCheck(string audience, string style, string text)
        {
            var prompt = $$"""
                Ты — эксперт по стилю текстов для социальных сетей.

                Проанализируй, насколько текст подходит под указанную аудиторию и стиль.
                Не оценивай орфографию и пунктуацию.
                Не пиши пример нового готового текста.
                Нужен только анализ и рекомендации.

                Целевая аудитория: {{audience}}
                Желаемый стиль: {{style}}
                Текст поста: "{{text}}"

                Верни ответ СТРОГО в формате JSON без markdown и без пояснений:

                {
        
                          "assessment": "соответствует",
                  "summary": "Краткий общий вывод",
                  "strengths": [
                    "Сильная сторона 1",
                    "Сильная сторона 2"
                  ],
                  "issues": [
                    "Проблема 1",
                    "Проблема 2"
                  ],
                  "recommendations": [
                    "Рекомендация 1",
                    "Рекомендация 2",
                    "Рекомендация 3"
                  ]
                }

                Допустимые значения поля assessment:
                - соответствует
                - частично соответствует
                - не соответствует
                """;

            var rawResponse = await CallLlmAsync(prompt, LlmMode.Strict);

            var json = ExtractJson(rawResponse);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<StyleCheckResultDto>(json, options);

            return result ?? new StyleCheckResultDto
            {
                Assessment = "не удалось определить",
                Summary = "Не удалось корректно обработать ответ модели.",
                Strengths = new List<string>(),
                Issues = new List<string>(),
                Recommendations = new List<string>()
            };
        }
    }
}

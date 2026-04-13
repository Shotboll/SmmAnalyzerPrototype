using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text;
using System.Text.Encodings.Web;
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

        public async Task<string> CallLlmAsync(string prompt, LlmMode mode, object? format = null)
        {
            var options = mode switch
            {
                LlmMode.Strict => new { temperature = 0.0, top_p = 0.1, num_predict = 500, num_ctx = 4096 },
                LlmMode.RAG => new { temperature = 0.0, top_p = 0.1, num_predict = 900, num_ctx = 4096 },
                LlmMode.Creative => new { temperature = 0.8, top_p = 0.9, num_predict = 700, num_ctx = 4096 },
                _ => new { temperature = 0.0, top_p = 0.1, num_predict = 500, num_ctx = 4096 }
            };

            var requestBody = new
            {
                model = _model,
                prompt = prompt,
                stream = false,
                format = format,
                options = options
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                Console.WriteLine("===== OLLAMA REQUEST =====");
                Console.WriteLine(json);

                var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);
                var result = await response.Content.ReadAsStringAsync();

                Console.WriteLine("===== OLLAMA RESPONSE STATUS =====");
                Console.WriteLine(response.StatusCode);

                Console.WriteLine("===== OLLAMA RAW RESPONSE =====");
                Console.WriteLine(result);

                response.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(result);

                if (!doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    Console.WriteLine("В ответе Ollama нет поля 'response'.");
                    return string.Empty;
                }

                var text = responseElement.GetString()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine("Ollama вернула пустой response.");

                    if (doc.RootElement.TryGetProperty("done_reason", out var doneReason))
                        Console.WriteLine($"done_reason: {doneReason}");

                    if (doc.RootElement.TryGetProperty("model", out var model))
                        Console.WriteLine($"model: {model}");

                    if (doc.RootElement.TryGetProperty("eval_count", out var evalCount))
                        Console.WriteLine($"eval_count: {evalCount}");

                    if (doc.RootElement.TryGetProperty("prompt_eval_count", out var promptEvalCount))
                        Console.WriteLine($"prompt_eval_count: {promptEvalCount}");
                }

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обращении к Ollama: {ex}");
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
            var searchText = $"Проверка поста на соответствие регламентам сообщества: {postText}";
            float[] queryEmbedding = await _embeddingService.GetEmbeddingAsync(searchText, isQuery: true);
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
                .Include(x => x.Regulation)
                .ToListAsync();

            var contextText = string.Join("\n\n", chunks.Select((x, i) =>
                $"[ Номер: {i + 1}]\n{x.ChunkText}"
            ));

            var prompt = $"""
                Ты — строгий модератор контента сообщества.

                Ниже приведены правила сообщества:
                {contextText}

                Пост для проверки:
                {postText}

                Задача:
                Определи, есть ли в посте ЯВНЫЕ нарушения правил.

                Критически важные правила:
                1. Не додумывай скрытый смысл.
                2. Не интерпретируй нейтральные деловые, технические или информационные формулировки как нарушение.
                3. Нарушение фиксируется только тогда, когда в тексте есть прямой и явный признак нарушения правила.
                4. Формулировки вида "может восприниматься как", "похоже на", "можно трактовать как", "косвенно указывает на" запрещены.
                5. Если нарушение нельзя подтвердить точной цитатой из поста и прямой связью с текстом правила — нарушения нет.
                6. Обычное описание процессов, технологий, тестирования, качества, разработки, публикации, анализа, автоматизации и проверки не является мошенничеством, обманом, манипуляцией или рекламой само по себе.
                7. Если текст просто описывает рабочий процесс, опыт, технологию или внутреннюю практику, это не нарушение.
                
                

                Заполни JSON строго по заданной схеме.
                """;

            string response = await CallLlmAsync(prompt, LlmMode.RAG, GetRegulationCheckSchema());

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

        public async Task<ExplainGrammarItemResponse> ExplainSingleGrammarErrorAsync(ExplainGrammarItemRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Fragment))
            {
                return new ExplainGrammarItemResponse
                {
                    Explanation = "Не удалось определить ошибку.",
                    Hint = "Проверьте этот фрагмент вручную."
                };
            }

            var promptObject = new
            {
                sentence = request.Sentence,
                fragment = request.Fragment,
                suggestion = request.Suggestion,
                type = request.Type,
                message = request.Message
            };

            var promptJson = JsonSerializer.Serialize(promptObject, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var prompt = $$"""
                Ты — помощник редактора русского текста.

                Тебе передана ОДНА уже найденная ошибка.
                Ошибка уже определена внешним инструментом.
                Твоя задача — не искать новую ошибку, а кратко и понятно объяснить пользователю, что не так и как исправить.

                Очень важные правила:
                1. Объясняй ТОЛЬКО на основе полей fragment, suggestion, type, message.
                2. Не придумывай новые причины ошибки.
                3. Не анализируй слово по буквам, если это прямо не следует из данных.
                4. Не пиши ложных утверждений вроде "вместо буквы X должна быть Y", если этого нельзя надёжно вывести.

                6. Если есть suggestion, совет должен опираться на него.
                
                8. Не добавляй ничего вне JSON.

                fragment - ЭТО МЕСТО ГДЕ НАХОДИТЬСЯ ОШИБКА
                suggestion - ЭТО ИСПРАВЛЕНИЕ ЭТОЙ ОШИБКИ
                message - ЭТО МИНИ ОБЪЯСНЕНИЕ ОШИБКИ
                sentence - ЭТО ПРЕДЛОЖЕНИЕ В КОТОРОМ НАХОДИТСЯ ОШИБКА, ОНО ПОМОЖЕТ ТЕБЕ ПОНЯТЬ КОНТЕКСТ И НАПИСАТЬ ПРАВИЛЬНОЕ ПОЯСНЕНИЕ

                Верни строго JSON-объект:
                {
                  "explanation": "Объяснение ошибки с правилами русского языка",
                  "hint": "Короткий совет по исправлению данной ошибки"
                }

                

                Данные:
                {{promptJson}}
                """;

            var schema = new
            {
                type = "object",
                properties = new
                {
                    explanation = new { type = "string" },
                    hint = new { type = "string" }
                },
                required = new[] { "explanation", "hint" }
            };

            var rawResponse = await CallLlmAsync(prompt, LlmMode.Strict, schema);

            try
            {
                var result = JsonSerializer.Deserialize<ExplainGrammarItemResponse>(rawResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null ||
                    string.IsNullOrWhiteSpace(result.Explanation) ||
                    string.IsNullOrWhiteSpace(result.Hint))
                {
                    throw new Exception("Пустой или некорректный ответ модели.");
                }

                return result;
            }
            catch
            {
                return BuildSafeGrammarExplanation(request);
            }
        }


        public async Task<List<GrammarExplanationDto>> ExplainGrammarErrorsAsync(string originalText, List<GrammarErrorDto> rawErrors)
        {
            if (string.IsNullOrWhiteSpace(originalText) || rawErrors == null || rawErrors.Count == 0)
                return new List<GrammarExplanationDto>();

            var limitedErrors = rawErrors.Take(12).ToList();

            var promptObject = new
            {
                text = originalText,
                errors = limitedErrors.Select((e, index) => new
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
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var prompt = $$"""
                Ты — редактор русского текста.
                Тебе уже передан список найденных ошибок.

                Не ищи новые ошибки.
                Не пропускай ошибки из списка.
                Для каждой ошибки:
                - кратко объясни проблему;
                - дай короткий совет по исправлению.

                Верни только JSON-массив:
                [
                  {
                    "index": 0,
                    "explanation": "Краткое объяснение",
                    "hint": "Короткий совет"
                  }
                ]

                Данные:
                {{promptJson}}
                """;

            var rawResponse = await CallLlmAsync(prompt, LlmMode.Strict, GetGrammarExplanationSchema());
            rawResponse = ExtractJsonGrammar(rawResponse);

            try
            {
                var result = JsonSerializer.Deserialize<List<GrammarExplanationDto>>(rawResponse);
                return result ?? new List<GrammarExplanationDto>();
            }
            catch
            {
                return limitedErrors.Select((e, i) => new GrammarExplanationDto
                {
                    Index = i,
                    Explanation = $"Ошибка в фрагменте \"{e.Fragment}\".",
                    Hint = !string.IsNullOrWhiteSpace(e.Message)
                        ? e.Message
                        : "Проверьте предложенное исправление."
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
        private static object GetRegulationCheckSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    hasViolations = new { type = "boolean" },
                    violations = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                ruleNumber = new { type = "integer" },
                                ruleShort = new { type = "string" },
                                matchedText = new { type = "string" },
                                explanation = new { type = "string" }
                            },
                            required = new[] { "ruleNumber", "ruleShort", "matchedText", "explanation" }
                        }
                    },
                    comment = new { type = "string" }
                },
                required = new[] { "hasViolations", "violations", "comment" }
            };
        }
        private static object GetGrammarExplanationSchema()
        {
            return new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "integer" },
                        explanation = new { type = "string" },
                        hint = new { type = "string" }
                    },
                    required = new[] { "index", "explanation", "hint" }
                }
            };
        }

        private static ExplainGrammarItemResponse BuildSafeGrammarExplanation(ExplainGrammarItemRequest request)
        {
            var fragment = request.Fragment?.Trim() ?? string.Empty;
            var suggestion = request.Suggestion?.Trim() ?? string.Empty;
            var message = request.Message?.Trim() ?? string.Empty;
            var type = request.Type?.Trim().ToLowerInvariant() ?? string.Empty;

            string NormalizeQuotes(string text) => text.Replace("\"", "«").Replace("'", "«");

            if (message.Contains("через дефис", StringComparison.OrdinalIgnoreCase))
            {
                return new ExplainGrammarItemResponse
                {
                    Explanation = $"Это сочетание нужно писать через дефис.",
                    Hint = !string.IsNullOrWhiteSpace(suggestion)
                        ? $"Используйте вариант: «{suggestion}»."
                        : "Проверьте написание через дефис."
                };
            }

            if (message.Contains("слово пишется слитно", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("пишется слитно", StringComparison.OrdinalIgnoreCase))
            {
                return new ExplainGrammarItemResponse
                {
                    Explanation = $"В этом случае сочетание пишется слитно.",
                    Hint = !string.IsNullOrWhiteSpace(suggestion)
                        ? $"Используйте вариант: «{suggestion}»."
                        : "Проверьте слитное написание."
                };
            }

            if (message.Contains("пропущена запятая", StringComparison.OrdinalIgnoreCase))
            {
                return new ExplainGrammarItemResponse
                {
                    Explanation = $"В этом месте пропущена запятая.",
                    Hint = !string.IsNullOrWhiteSpace(suggestion)
                        ? $"Корректный вариант: «{suggestion}»."
                        : "Проверьте пунктуацию в этом фрагменте."
                };
            }

            if (message.Contains("должно быть", StringComparison.OrdinalIgnoreCase))
            {
                return new ExplainGrammarItemResponse
                {
                    Explanation = $"В этом выражении используется неверная форма.",
                    Hint = !string.IsNullOrWhiteSpace(suggestion)
                        ? $"Правильный вариант: «{suggestion}»."
                        : message
                };
            }

            if (message.Contains("орфографическая ошибка", StringComparison.OrdinalIgnoreCase) || type == "misspelling")
            {
                if (!string.IsNullOrWhiteSpace(suggestion))
                {
                    return new ExplainGrammarItemResponse
                    {
                        Explanation = $"Во фрагменте «{fragment}» есть ошибка в написании.",
                        Hint = $"Правильный вариант: «{suggestion}»."
                    };
                }

                return new ExplainGrammarItemResponse
                {
                    Explanation = $"Во фрагменте «{fragment}» есть вероятная орфографическая ошибка.",
                    Hint = "Проверьте написание этого слова."
                };
            }

            return new ExplainGrammarItemResponse
            {
                Explanation = $"Во фрагменте «{fragment}» обнаружена языковая неточность.",
                Hint = !string.IsNullOrWhiteSpace(suggestion)
                    ? $"Проверьте вариант: «{suggestion}»."
                    : (!string.IsNullOrWhiteSpace(message) ? message : "Проверьте этот фрагмент вручную.")
            };
        }
        
    }
}

using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
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

        public enum LlmMode { Strict, Creative, RAG, Forecast }

        public async Task<string> CallLlmAsync(string prompt, LlmMode mode, object? format = null)
        {
            var options = mode switch
            {
                LlmMode.Strict => new { temperature = 0.0, top_p = 0.1, num_predict = 500, num_ctx = 4096 },
                LlmMode.RAG => new { temperature = 0.0, top_p = 0.1, num_predict = 900, num_ctx = 4096 },
                LlmMode.Creative => new { temperature = 0.8, top_p = 0.9, num_predict = 700, num_ctx = 4096 },
                LlmMode.Forecast => new { temperature = 0.3, top_p = 0.85, num_predict = 600, num_ctx = 4096 },
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

        /// <summary>
        /// Расширенный прогноз вовлеченности на основе истории сообщества.
        /// Соответствует функции 8 (п. 1.3.2 ВКР) и алгоритму 2.3.3 (Рис. 5).
        /// </summary>
        public async Task<EngagementForecastDto> ForecastEngagementAsync(
            Guid communityId,
            string newPostText,
            int historyDays = 30,
            CancellationToken ct = default)
        {
            var (metrics, topPosts, community) = await LoadContextAsync(communityId, newPostText, historyDays, ct);

            var prompt = $$"""
                    Ты — аналитик социальных сетей. Оцени потенциальную вовлеченность поста ДЛЯ КОНКРЕТНОГО СООБЩЕСТВА.

                    ПРОФИЛЬ СООБЩЕСТВА:
                    • Целевая аудитория: {{community.TargetAudience}}
                    • Стиль общения: {{community.StyleProfile}}
                    • Средние метрики (30 дней): лайки {{metrics.AvgLikes}}, комментарии {{metrics.AvgComments}}, просмотры {{metrics.AvgViews}}, ER {{metrics.AvgER}}%
                    • Топ-3 поста по вовлеченности:
                    {{string.Join("\n", topPosts.Select((p, i) => $"{i + 1}. «{p.ShortText}» (лайки {p.Likes}, комментарии {p.Comments})"))}}

                    НОВЫЙ ТЕКСТ:
                    {{newPostText}}

                    ❗ КРИТЕРИИ ОЦЕНКИ (оценивай ОТНОСИТЕЛЬНО профиля сообщества, а не абсолютно):

                    СНИЖАЮТ оценку (–15…–40 баллов), если есть ЛЮБОЙ из признаков:
                    - Контент не соответствует тематике/интересам указанной ЦА
                    - Тон/стиль текста противоречит заявленному стилю общения сообщества
                    - Отсутствие ясной цели или призыва к действию, релевантного данной аудитории
                    - Избыточная сложность или, наоборот, примитивность для данной ЦА
                    - Контент, который может вызвать отторжение у данной аудитории (определи по контексту)

                    ПОВЫШАЮТ оценку (+15…+40 баллов), если есть:
                    - Явное соответствие теме/интересам ЦА и стилю сообщества
                    - Конкретные факты, цифры, доказательства, релевантные для данной аудитории
                    - Четкий призыв к действию, уместный для этого сообщества
                    - Хорошая структура: заголовок, логика, визуальные акценты (если уместно по стилю)
                    - Эмоциональная подача, соответствующая ожидаемому тону сообщества

                    ПРАВИЛА:
                    1. Не оценивай текст «вообще». Оценивай: «Насколько этот текст подойдет именно ЭТОЙ аудитории в ЭТОМ стиле?»
                    2. Сравни пост с топ-3. Если по теме/стилю/подаче близок к лидеру → 75–95. Если выбивается из контекста → <40.
                    3. «Политика», «коммерция», «юмор» — не являются автоматически «плохими» или «хорошими». Оценивай их уместность для данного сообщества.
                    4. Если в тексте есть явные ошибки, токсичность или спам — снижай оценку независимо от тематики.

                    ШКАЛА (используй ВЕСЬ диапазон 0–100):
                    0–20  | Критическое несоответствие: контент чужероден для ЦА/стиля, есть ошибки/спам
                    21–40 | Низкий: текст уместен, но нет ценности, структуры или призыва для этой ЦА
                    41–60 | Средний: стандартный пост, соответствует профилю, но без «крючка»
                    61–80 | Высокий: хорошая структура, ценность, элементы вовлечения, соответствует стилю
                    81–100| Отличный: идеально под ЦА + структура + факты/эмоции + четкий уместный призыв

                    ВЕРНИ СТРОГО JSON:
                    {
                      "level": "string", // ОЧЕНЬ НИЗКИЙ / НИЗКИЙ / СРЕДНИЙ / ВЫСОКИЙ / ОЧЕНЬ ВЫСОКИЙ
                      "quality_score": int, // 0–100, используй весь диапазон
                      "expected_likes": int,
                      "expected_comments": int,
                      "expected_views": int,
                      "expected_er_percent": double,
                      "confidence": double,
                      "reasoning": "string", // Объясни, почему оценка такая, ссылаясь на ЦА и стиль
                      "key_factors": ["string"], // 2-3 фактора, влияющих на прогноз
                      "risks": ["string"], // 1-2 риска для вовлеченности
                      "comparison_with_avg": "string" // Вид: "+15% к лайкам" или "ниже среднего из-за..."
                    }
                    """;

            var schema = new
            {
                type = "object",
                properties = new
                {
                    level = new { type = "string" },
                    expected_likes = new { type = "integer" },
                    quality_score = new { type = "integer" },
                    expected_comments = new { type = "integer" },
                    expected_views = new { type = "integer" },
                    expected_er_percent = new { type = "number" },
                    confidence = new { type = "number" },
                    reasoning = new { type = "string" },
                    key_factors = new { type = "array", items = new { type = "string" } },
                    risks = new { type = "array", items = new { type = "string" } },
                    comparison_with_avg = new { type = "string" }
                },
                required = new[] { "level", "quality_score", "expected_likes", "expected_comments", "expected_views", "expected_er_percent", "confidence", "reasoning", "key_factors", "risks", "comparison_with_avg" }
            };

            return await ParseLlmResponseAsync<EngagementForecastDto>(prompt, schema, ct);
        }

        /// <summary>
        /// Генерация рекомендаций по улучшению текста и подбору тем.
        /// Соответствует функции 9 (п. 1.3.2 ВКР) и алгоритму 2.3.3 (Рис. 5).
        /// </summary>
        public async Task<ContentRecommendationsDto> GenerateRecommendationsAsync(
            Guid communityId,
            string newPostText,
            int historyDays = 30,
            CancellationToken ct = default)
        {
            var (metrics, topPosts, community) = await LoadContextAsync(communityId, newPostText, historyDays, ct);

            var prompt = $$"""
                    Ты — SMM-стратег и редактор. Дай практические рекомендации по улучшению поста.

                    КОНТЕКСТ:
                    • ЦА: {{community?.TargetAudience ?? "не указана"}}
                    • Стиль: {{community?.StyleProfile ?? "не указан"}}
                    • Средние метрики: лайки {{metrics.AvgLikes:F0}}, комментарии {{metrics.AvgComments:F0}}, просмотры {{metrics.AvgViews:F0}}
                    • Что обычно работает: {{string.Join(", ", topPosts.Take(2).Select(p => $"«{p.ShortText}»"))}}

                    ТЕКСТ ДЛЯ УЛУЧШЕНИЯ:
                    {{newPostText}}

                    ЗАДАЧА:
                    1. Конкретные правки текста (лексика, тон, ясность).
                    2. Структурные изменения (заголовок, абзацы, CTA).
                    3. 2-3 смежные темы, которые могут сработать.
                    4. Приёмы повышения вовлеченности (опрос, вопрос, медиа, хештеги).
                    5. Общий совет по публикации.

                    Верни СТРОГО JSON:
                    {
                        "text_improvements": ["string"],
                        "structural_changes": ["string"],
                        "topic_ideas": ["string"],
                        "engagement_boosters": ["string"],
                        "overall_advice": "string"
                    }
                    """;

            var schema = new
            {
                type = "object",
                properties = new
                {
                    textImprovements = new { type = "array", items = new { type = "string" }, minItems = 3 },
                    structuralChanges = new { type = "array", items = new { type = "string" }, minItems = 2 },
                    topicIdeas = new { type = "array", items = new { type = "string" }, minItems = 2 },
                    engagementBoosters = new { type = "array", items = new { type = "string" }, minItems = 2 },
                    overallAdvice = new { type = "string" }
                },
                required = new[] { "textImprovements", "structuralChanges", "topicIdeas", "engagementBoosters", "overallAdvice" }
            };

            return await ParseLlmResponseAsync<ContentRecommendationsDto>(prompt, schema, ct);
        }

        private async Task<(CommunityMetricsDto Metrics, List<SamplePostDto> TopPosts, Community? Community)>LoadContextAsync(Guid communityId, string text, int historyDays, CancellationToken ct)
        {
            var cutoff = DateTime.UtcNow.AddDays(-historyDays);
            var posts = await _context.CommunityPosts
                .Where(p => p.CommunityId == communityId && p.PublishedAt >= cutoff)
                .OrderByDescending(p => p.PublishedAt)
                .ToListAsync(ct);

            var total = posts.Count;
            var avgLikes = total > 0 ? posts.Average(p => p.Likes) : 0;
            var avgComments = total > 0 ? posts.Average(p => p.Comments) : 0;
            var avgViews = total > 0 ? posts.Average(p => p.Views) : 0;
            var avgER = avgViews > 0 ? (avgLikes + avgComments * 2) / avgViews * 100 : 0;

            var metrics = new CommunityMetricsDto
            {
                TotalPosts = total,
                PostsPerWeek = total / (historyDays / 7.0),
                AvgLikes = Math.Round(avgLikes, 1),
                AvgComments = Math.Round(avgComments, 1),
                AvgViews = Math.Round(avgViews, 0),
                AvgER = Math.Round(avgER, 2)
            };

            var topPosts = posts
                .OrderByDescending(p => p.Likes + p.Comments * 2)
                .Take(3)
                .Select(p => new SamplePostDto
                {
                    ShortText = p.Text,
                    Likes = p.Likes,
                    Comments = p.Comments
                })
                .ToList();

            var community = await _context.Communities.FindAsync(communityId, ct);
            return (metrics, topPosts, community);
        }

        private async Task<T> ParseLlmResponseAsync<T>(string prompt, object schema, CancellationToken ct)
        {
            // 1. Получаем сырой ответ от Ollama
            var raw = await CallLlmAsync(prompt, LlmMode.Forecast, schema);

            // 2. CallLlmAsync уже извлек поле "response" из wrapper-ответа Ollama
            //    Но если там остался внешний JSON-обёртка — извлекаем внутренний ответ
             var json = raw.Trim();

            // Если ответ начинается с { и содержит поле "response" — это wrapper Ollama
            if (json.StartsWith("{") && json.Contains("\"response\""))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("response", out var responseElement))
                    {
                        json = responseElement.GetString()?.Trim() ?? json;
                    }
                }
                catch
                {
                    // Если не получилось распарсить — пробуем как есть
                }
            }

            // 3. Пробуем десериализовать
            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null) return result;
            }
            catch (Exception ex)
            {
                // Логируем ошибку для отладки
                Console.WriteLine($"❌ Ошибка десериализации: {ex.Message}");
                Console.WriteLine($"📦 JSON: {json}");
            }

            // 4. Fallback: возвращаем пустой объект с предупреждением
            var fallback = Activator.CreateInstance<T>()!;
            var typeName = typeof(T).Name;

            if (typeName.Contains("Forecast"))
            {
                ((dynamic)fallback).Level = "ошибка парсинга";
                ((dynamic)fallback).Reasoning = "Не удалось обработать ответ модели";
            }
            else if (typeName.Contains("Recommendation"))
            {
                ((dynamic)fallback).OverallAdvice = "Попробуйте переформулировать запрос";
            }

            return fallback;
        }
    }
}

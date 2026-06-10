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

            if (!chunks.Any())
            {
                return JsonSerializer.Serialize(new AnalyzePostResponse
                {
                    HasViolations = false,
                    Violations = new List<ViolationDto>(),
                    Comment = "Для выбранного сообщества не найдены регламенты, поэтому проверка по правилам не выполнялась."
                });
            }

            var contextText = string.Join("\n\n", chunks.Select((x, i) =>
                $"[Правило {i + 1}]\nНазвание регламента: {x.Regulation?.Title}\nФрагмент правила:\n{x.ChunkText}"
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

                ВАЖНО:
                - Проверяй пост ТОЛЬКО по правилам, приведенным выше.
                - Не используй свои собственные правила модерации.
                - Если в приведенных правилах нет запрета, связанного с текстом поста, верни hasViolations = false.
                - Не добавляй ничего вне JSON.

                Заполни JSON строго по заданной схеме.
                """;

            string response = await CallLlmAsync(prompt, LlmMode.RAG, GetRegulationCheckSchema());

            response = LlmJsonExtractor.ExtractJson(response);

            return response;
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
            rawResponse = LlmJsonExtractor.ExtractJsonGrammar(rawResponse);

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

            var json = LlmJsonExtractor.ExtractJson(rawResponse);

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
        /// </summary>
        public async Task<EngagementForecastDto> ForecastEngagementAsync(Guid communityId, string newPostText, int historyDays = 30, CancellationToken ct = default)
        {
            var (metrics, topPosts, community) = await LoadContextAsync(communityId, newPostText, historyDays, ct);

            var topPostsText = topPosts.Count > 0
                ? string.Join("\n", topPosts.Select((p, i) => $"{i + 1}. «{p.ShortText}» (лайки {p.Likes}, комментарии {p.Comments})"))
                : "Недостаточно исторических публикаций для выделения топ-постов.";

            var prompt = $$"""
                Ты — аналитик социальных сетей. Оцени потенциальную вовлеченность поста ДЛЯ КОНКРЕТНОГО СООБЩЕСТВА.

                ПРОФИЛЬ СООБЩЕСТВА:
                • Название: {{community.Name}}
                • Целевая аудитория: {{community.TargetAudience}}
                • Стиль общения: {{community.StyleProfile}}

                ИСТОРИЯ ПУБЛИКАЦИЙ:
                • Количество учтенных постов: {{metrics.TotalPosts}}
                • Средние метрики: лайки {{metrics.AvgLikes}}, комментарии {{metrics.AvgComments}}, просмотры {{metrics.AvgViews}}, ER {{metrics.AvgER}}%
                • Топ-3 поста по вовлеченности:
                {{topPostsText}}

                НОВЫЙ ТЕКСТ:
                {{newPostText}}

                ЗАДАЧА:
                Оцени, насколько новый текст может быть успешен именно для этого сообщества.
                Оцени не абстрактно, а относительно целевой аудитории, стиля общения и истории публикаций.

                КРИТЕРИИ ОЦЕНКИ:
                1. Соответствие теме и интересам целевой аудитории.
                2. Соответствие стилю общения сообщества.
                3. Наличие понятной ценности для аудитории.
                4. Наличие структуры, конкретики и призыва к действию.
                5. Потенциальная способность вызвать реакцию: лайк, комментарий, репост.

                ШКАЛА qualityScore:
                0–20  — критически низкое соответствие;
                21–40 — низкий потенциал;
                41–60 — средний потенциал;
                61–80 — высокий потенциал;
                81–100 — очень высокий потенциал.

                ВАЖНО:
                - Не завышай оценку без причины.
                - Если исторических данных мало, укажи это в reasoning и confidence.
                - expectedLikes, expectedComments и expectedViews должны быть реалистично связаны со средними метриками.
                - Если текст хуже среднего, прогноз должен быть ниже средних метрик.
                - Если текст лучше среднего, прогноз может быть выше средних метрик.

                Верни СТРОГО JSON без markdown:
                {
                  "level": "ОЧЕНЬ НИЗКИЙ / НИЗКИЙ / СРЕДНИЙ / ВЫСОКИЙ / ОЧЕНЬ ВЫСОКИЙ",
                  "quality_score": 0,
                  "expected_likes": 0,
                  "expected_comments": 0,
                  "expected_views": 0,
                  "expected_er_percent": 0.0,
                  "confidence": 0.0,
                  "reasoning": "Краткое объяснение прогноза",
                  "key_factors": ["Фактор 1", "Фактор 2"],
                  "risks": ["Риск 1"],
                  "comparison_with_avg": "Сравнение со средними метриками сообщества"
                }
                """;

            var schema = new
            {
                type = "object",
                properties = new
                {
                    level = new { type = "string" },
                    quality_score = new { type = "integer" },
                    expected_likes = new { type = "integer" },
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
        /// </summary>
        public async Task<ContentRecommendationsDto> GenerateRecommendationsAsync(Guid communityId, string newPostText, int historyDays = 30, CancellationToken ct = default)
        {
            var (metrics, topPosts, community) = await LoadContextAsync(communityId, newPostText, historyDays, ct);

            var topPostsText = topPosts.Count > 0
                ? string.Join("\n", topPosts.Take(3).Select((p, i) => $"{i + 1}. «{p.ShortText}»"))
                : "Недостаточно исторических публикаций для выделения успешных тем.";

            var prompt = $$"""
                Ты — SMM-стратег и редактор. Дай практические рекомендации по улучшению поста для конкретного сообщества.

                ПРОФИЛЬ СООБЩЕСТВА:
                • Название: {{community.Name}}
                • Целевая аудитория: {{community.TargetAudience}}
                • Стиль общения: {{community.StyleProfile}}

                ИСТОРИЯ ПУБЛИКАЦИЙ:
                • Количество учтенных постов: {{metrics.TotalPosts}}
                • Средние метрики: лайки {{metrics.AvgLikes}}, комментарии {{metrics.AvgComments}}, просмотры {{metrics.AvgViews}}, ER {{metrics.AvgER}}%
                • Что обычно работает:
                {{topPostsText}}

                ТЕКСТ ДЛЯ УЛУЧШЕНИЯ:
                {{newPostText}}

                ЗАДАЧА:
                1. Дай конкретные правки текста.
                2. Предложи структурные изменения.
                3. Предложи 2-3 смежные темы для будущих публикаций.
                4. Предложи приемы повышения вовлеченности.
                5. Дай общий совет по публикации.

                ВАЖНО:
                - Не давай слишком общие советы вроде "сделайте текст интереснее".
                - Каждая рекомендация должна быть применима к данному тексту.
                - Учитывай целевую аудиторию и стиль сообщества.
                - Не переписывай весь пост полностью.
                - Не добавляй ничего вне JSON.

                Верни СТРОГО JSON без markdown:
                {
                  "textImprovements": ["Конкретная правка текста 1", "Конкретная правка текста 2", "Конкретная правка текста 3"],
                  "structuralChanges": ["Структурное изменение 1", "Структурное изменение 2"],
                  "topicIdeas": ["Тема 1", "Тема 2", "Тема 3"],
                  "engagementBoosters": ["Прием вовлеченности 1", "Прием вовлеченности 2"],
                  "overallAdvice": "Общий совет по публикации"
                }
                """;

            var schema = new
            {
                type = "object",
                properties = new
                {
                    textImprovements = new { type = "array", items = new { type = "string" } },
                    structuralChanges = new { type = "array", items = new { type = "string" } },
                    topicIdeas = new { type = "array", items = new { type = "string" } },
                    engagementBoosters = new { type = "array", items = new { type = "string" } },
                    overallAdvice = new { type = "string" }
                },
                required = new[] { "textImprovements", "structuralChanges", "topicIdeas", "engagementBoosters", "overallAdvice" }
            };

            return await ParseLlmResponseAsync<ContentRecommendationsDto>(prompt, schema, ct);
        }

        private async Task<(CommunityMetricsDto Metrics, List<SamplePostDto> TopPosts, Community Community)> LoadContextAsync(Guid communityId, string text, int historyDays, CancellationToken ct)
        {
            var community = await _context.Communities.FirstOrDefaultAsync(c => c.Id == communityId, ct);

            if (community == null)
                throw new InvalidOperationException("Сообщество не найдено.");

            var cutoff = DateTime.UtcNow.AddDays(-historyDays);

            var posts = await _context.CommunityPosts
                .Where(p => p.CommunityId == communityId)
                .Where(p => p.Source == "vk")
                .Where(p => p.PublishedAt >= cutoff)
                .OrderByDescending(p => p.PublishedAt)
                .ToListAsync(ct);

            if (posts.Count < 10)
            {
                posts = await _context.CommunityPosts
                    .Where(p => p.CommunityId == communityId)
                    .Where(p => p.Source == "vk")
                    .OrderByDescending(p => p.PublishedAt)
                    .Take(50)
                    .ToListAsync(ct);
            }

            var total = posts.Count;

            var avgLikes = total > 0 ? posts.Average(p => p.Likes) : 0;
            var avgComments = total > 0 ? posts.Average(p => p.Comments) : 0;
            var avgReposts = total > 0 ? posts.Average(p => p.Reposts) : 0;
            var avgViews = total > 0 ? posts.Average(p => p.Views) : 0;

            var avgER = avgViews > 0
                ? (avgLikes + avgComments + avgReposts) / avgViews * 100
                : 0;

            var metrics = new CommunityMetricsDto
            {
                TotalPosts = total,
                PostsPerWeek = historyDays > 0 ? Math.Round(total / (historyDays / 7.0), 2) : 0,
                AvgLikes = Math.Round(avgLikes, 1),
                AvgComments = Math.Round(avgComments, 1),
                AvgViews = Math.Round(avgViews, 0),
                AvgER = Math.Round(avgER, 2)
            };

            var topPosts = posts
                .OrderByDescending(p => p.Views > 0 ? ((p.Likes + p.Comments + p.Reposts) / (double)p.Views) : (p.Likes + p.Comments * 2 + p.Reposts * 3))
                .Take(3)
                .Select(p => new SamplePostDto
                {
                    ShortText = TruncateText(p.Text, 280),
                    Likes = p.Likes,
                    Comments = p.Comments
                })
                .ToList();

            return (metrics, topPosts, community);
        }

        private static string TruncateText(string? text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim();

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength).Trim() + "...";
        }

        private async Task<T> ParseLlmResponseAsync<T>(string prompt, object schema, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var raw = await CallLlmAsync(prompt, LlmMode.Forecast, schema);

            ct.ThrowIfCancellationRequested();

            var json = LlmJsonExtractor.ExtractJson(raw);

            try
            {
                var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null)
                    return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка десериализации ответа модели: {ex.Message}");
                Console.WriteLine($"JSON: {json}");
            }

            if (typeof(T) == typeof(EngagementForecastDto))
            {
                var fallback = new EngagementForecastDto
                {
                    Level = "ошибка парсинга",
                    QualityScore = 0,
                    ExpectedLikes = 0,
                    ExpectedComments = 0,
                    ExpectedViews = 0,
                    ExpectedERPercent = 0,
                    Confidence = 0,
                    Reasoning = "Не удалось корректно обработать ответ модели.",
                    KeyFactors = new List<string>(),
                    Risks = new List<string>(),
                    ComparisonWithAvg = "Сравнение со средними метриками недоступно."
                };

                return (T)(object)fallback;
            }

            if (typeof(T) == typeof(ContentRecommendationsDto))
            {
                var fallback = new ContentRecommendationsDto
                {
                    TextImprovements = new List<string>(),
                    StructuralChanges = new List<string>(),
                    TopicIdeas = new List<string>(),
                    EngagementBoosters = new List<string>(),
                    OverallAdvice = "Не удалось корректно обработать ответ модели. Попробуйте запустить генерацию повторно."
                };

                return (T)(object)fallback;
            }

            return Activator.CreateInstance<T>()!;
        }
    }
}

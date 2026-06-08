using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text.Json;
using VkNet.Model;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PostApiController : Controller
    {
        private readonly AppDbContext _context;
        private readonly LlmService _llmService;
        private readonly LanguageToolService _languageToolService;
        private readonly GrammarFalsePositiveFilterService _grammarFilterService;
        private readonly VkService _vkService;
        private readonly ILogger<PostApiController> _logger;

        public PostApiController(AppDbContext context, LlmService llmService, LanguageToolService languageToolService, GrammarFalsePositiveFilterService grammarFilterService, VkService vkService, ILogger<PostApiController> logger)
        {
            _context = context;
            _llmService = llmService;
            _languageToolService = languageToolService;
            _grammarFilterService = grammarFilterService;
            _vkService = vkService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<PostListItemDto>>> GetAll()
        {

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            var posts = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.AnalysisResult)
                .Where(p => p.Community.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var result = posts.Select(p => new PostListItemDto
            {
                Id = p.Id,
                TextPreview = p.Text.Length > 140 ? p.Text.Substring(0, 140) + "..." : p.Text,
                CommunityName = p.Community.Name,
                CreatedAt = p.CreatedAt,
                Status = p.Status,
                GrammarChecked = p.AnalysisResult?.GrammarCheckedAt != null,
                StyleChecked = p.AnalysisResult?.StyleCheckedAt != null,
                RegulationChecked = p.AnalysisResult?.RegulationCheckedAt != null,
                ForecastChecked = p.AnalysisResult?.ForecastCheckedAt != null,
                RecommendationsChecked = p.AnalysisResult?.RecommendationsCheckedAt != null
            }).ToList();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PostDetailsDto>> GetById(Guid id)
        {

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            var post = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.AnalysisResult)
                    .ThenInclude(a => a.GrammarErrors)
                .Include(p => p.AnalysisResult)
                    .ThenInclude(a => a.ProhibitedTopicMatches)
                .FirstOrDefaultAsync(p => p.Id == id && p.Community.UserId == userId.Value);

            if (post == null)
                return NotFound();

            var dto = new PostDetailsDto
            {
                Id = post.Id,
                Text = post.Text,
                CommunityId = post.CommunityId,
                CommunityName = post.Community.Name,
                CreatedAt = post.CreatedAt,
                Status = post.Status,
                GrammarCheckedAt = post.AnalysisResult?.GrammarCheckedAt,
                StyleCheckedAt = post.AnalysisResult?.StyleCheckedAt,
                RegulationCheckedAt = post.AnalysisResult?.RegulationCheckedAt,
                StyleAssessment = post.AnalysisResult?.StyleAssessment,
                StyleSummary = post.AnalysisResult?.StyleSummary,
                HasRegulationViolations = post.AnalysisResult?.HasRegulationViolations,
                RegulationComment = post.AnalysisResult?.RegulationComment,
                ForecastCheckedAt = post.AnalysisResult?.ForecastCheckedAt,
                RecommendationsCheckedAt = post.AnalysisResult?.RecommendationsCheckedAt,
            };

            if (!string.IsNullOrWhiteSpace(post.AnalysisResult?.StyleStrengthsJson))
            {
                dto.StyleStrengths = JsonSerializer.Deserialize<List<string>>(post.AnalysisResult.StyleStrengthsJson) ?? new List<string>();
            }

            if (!string.IsNullOrWhiteSpace(post.AnalysisResult?.StyleIssuesJson))
            {
                dto.StyleIssues = JsonSerializer.Deserialize<List<string>>(post.AnalysisResult.StyleIssuesJson) ?? new List<string>();
            }

            if (!string.IsNullOrWhiteSpace(post.AnalysisResult?.StyleRecommendationsJson))
            {
                dto.StyleRecommendations = JsonSerializer.Deserialize<List<string>>(post.AnalysisResult.StyleRecommendationsJson) ?? new List<string>();
            }

            if (post.AnalysisResult?.GrammarErrors != null)
            {
                dto.GrammarErrors = post.AnalysisResult.GrammarErrors
                    .OrderBy(x => x.Position)
                    .Select(x => new GrammarErrorDto
                    {
                        Fragment = x.Fragment,
                        Suggestion = x.Suggestion ?? string.Empty,
                        Type = x.ErrorType ?? string.Empty,
                        Offset = x.Position,
                        Length = 0,
                        Message = x.Message ?? string.Empty,
                        IsSuspicious = x.IsSuspicious,
                        Sentence = ExtractSentenceByOffset(post.Text, x.Position)
                    })
                    .ToList();
            }

            if (post.AnalysisResult?.ProhibitedTopicMatches != null)
            {
                dto.RegulationViolations = post.AnalysisResult.ProhibitedTopicMatches
                    .Select(x => new ViolationDto
                    {
                        RuleNumber = 0,
                        RuleShort = x.Topic,
                        MatchedText = x.Evidence,
                        Explanation = x.Explanation
                    })
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(post.AnalysisResult?.EngagementForecastJson))
            {
                try
                {
                    var forecast = JsonSerializer.Deserialize<EngagementForecastDto>(
                        post.AnalysisResult.EngagementForecastJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (forecast != null)
                    {
                        dto.ForecastLevel = forecast.Level;
                        dto.ForecastQualityScore = forecast.QualityScore;
                        dto.ForecastLikes = forecast.ExpectedLikes;
                        dto.ForecastComments = forecast.ExpectedComments;
                        dto.ForecastViews = forecast.ExpectedViews;
                        dto.ForecastERPercent = forecast.ExpectedERPercent;
                        dto.ForecastReasoning = forecast.Reasoning;
                        dto.ForecastKeyFactors = forecast.KeyFactors ?? new();
                        dto.ForecastRisks = forecast.Risks ?? new();
                        dto.ForecastComparison = forecast.ComparisonWithAvg;
                    }
                }
                catch { /* Игнорируем ошибки парсинга старого формата */ }
            }

            if (!string.IsNullOrWhiteSpace(post.AnalysisResult?.RecommendationsJson))
            {
                try
                {
                    var recs = JsonSerializer.Deserialize<ContentRecommendationsDto>(
                        post.AnalysisResult.RecommendationsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (recs != null)
                    {
                        dto.RecommendationsText = recs.TextImprovements ?? new();
                        dto.RecommendationsStruct = recs.StructuralChanges ?? new();
                        dto.RecommendationsTopics = recs.TopicIdeas ?? new();
                        dto.RecommendationsBoosters = recs.EngagementBoosters ?? new();
                        dto.RecommendationsAdvice = recs.OverallAdvice;
                    }
                }
                catch { /* Игнорируем ошибки парсинга старого формата */ }
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PostDetailsDto>> Create([FromBody] CreatePostRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            if (request == null || request.CommunityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Некорректные данные публикации.");

            var community = await _context.Communities
                .FirstOrDefaultAsync(c => c.Id == request.CommunityId && c.UserId == userId.Value);

            if (community == null)
                return BadRequest("Сообщество не найдено или недоступно текущему пользователю.");

            var post = new Data.Models.Post
            {
                Id = Guid.NewGuid(),
                Text = request.Text.Trim(),
                CommunityId = community.Id,
                CreatedAt = DateTime.UtcNow,
                Status = "Черновик"
            };

            var analysisResult = new AnalysisResult
            {
                PostId = post.Id,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            _context.AnalysisResults.Add(analysisResult);

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = post.Id }, new PostDetailsDto
            {
                Id = post.Id,
                Text = post.Text,
                CommunityId = post.CommunityId,
                CommunityName = community.Name,
                CreatedAt = post.CreatedAt,
                Status = post.Status
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePostRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            if (request == null || request.CommunityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Некорректные данные публикации.");

            var targetCommunityExists = await _context.Communities
                .AnyAsync(c => c.Id == request.CommunityId && c.UserId == userId.Value);

            if (!targetCommunityExists)
                return BadRequest("Сообщество не найдено или недоступно текущему пользователю.");

            var post = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == id && p.Community.UserId == userId.Value);

            if (post == null)
                return NotFound("Пост не найден.");

            var textChanged = post.Text.Trim() != request.Text.Trim();
            var communityChanged = post.CommunityId != request.CommunityId;

            post.Text = request.Text.Trim();
            post.CommunityId = request.CommunityId;
            post.UpdatedAt = DateTime.UtcNow;

            if (textChanged || communityChanged)
            {
                post.Status = "Черновик";

                if (post.AnalysisResult != null)
                {
                    var grammarErrors = await _context.GrammarErrors
                        .Where(x => x.AnalysisResultId == post.AnalysisResult.PostId)
                        .ToListAsync();

                    var prohibitedMatches = await _context.ProhibitedTopicMatches
                        .Where(x => x.AnalysisResultId == post.AnalysisResult.PostId)
                        .ToListAsync();

                    _context.GrammarErrors.RemoveRange(grammarErrors);
                    _context.ProhibitedTopicMatches.RemoveRange(prohibitedMatches);

                    post.AnalysisResult.GrammarCheckedAt = null;
                    post.AnalysisResult.StyleCheckedAt = null;
                    post.AnalysisResult.RegulationCheckedAt = null;
                    post.AnalysisResult.ForecastCheckedAt = null;
                    post.AnalysisResult.RecommendationsCheckedAt = null;

                    post.AnalysisResult.StyleAssessment = null;
                    post.AnalysisResult.StyleSummary = null;
                    post.AnalysisResult.StyleStrengthsJson = null;
                    post.AnalysisResult.StyleIssuesJson = null;
                    post.AnalysisResult.StyleRecommendationsJson = null;

                    post.AnalysisResult.HasRegulationViolations = null;
                    post.AnalysisResult.RegulationComment = null;
                    post.AnalysisResult.EngagementForecastJson = null;
                    post.AnalysisResult.RecommendationsJson = null;

                    post.AnalysisResult.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<ExplainGrammarItemResponse>> ExplainGrammarItem([FromBody] ExplainGrammarItemRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Fragment))
                return BadRequest();

            var result = await _llmService.ExplainSingleGrammarErrorAsync(request);
            return Ok(result);
        }

        [HttpPost("{postId}")]
        public async Task<ActionResult<EnhancedGrammarResponse>> RunGrammarCheck(Guid postId)
        {
            var post = await _context.Posts
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null)
                return NotFound();

            var rawErrors = await _languageToolService.CheckTextAsync(post.Text);
            var filterResult = _grammarFilterService.Filter(rawErrors, post.Text);

            var acceptedErrors = filterResult.AcceptedErrors;

            //var explanations = await _llmService.ExplainGrammarErrorsAsync(post.Text, acceptedErrors);
            var explanations = new List<GrammarExplanationDto>();
            var explanationMap = explanations.ToDictionary(x => x.Index, x => x);

            var cards = acceptedErrors.Select((error, index) =>
            {
                explanationMap.TryGetValue(index, out var explanation);

                return new GrammarResultCardDto
                {
                    Original = error.Fragment ?? string.Empty,
                    Correction = error.Suggestion ?? string.Empty,
                    Type = error.Type ?? string.Empty,
                    Hint = explanation?.Hint ?? error.Message ?? string.Empty,
                    Explanation = explanation?.Explanation ?? string.Empty,
                    Offset = error.Offset,
                    Length = error.Length,
                    IsSuspicious = false,
                    Sentence = ExtractSentenceByOffset(post.Text, error.Offset)
                };
            }).ToList();

            if (post.AnalysisResult == null)
            {
                post.AnalysisResult = new AnalysisResult
                {
                    PostId = post.Id,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.AnalysisResults.Add(post.AnalysisResult);
            }

            var oldErrors = await _context.GrammarErrors
                .Where(x => x.AnalysisResultId == post.AnalysisResult.PostId)
                .ToListAsync();

            _context.GrammarErrors.RemoveRange(oldErrors);

            foreach (var card in cards)
            {
                _context.GrammarErrors.Add(new GrammarError
                {
                    Id = Guid.NewGuid(),
                    AnalysisResultId = post.AnalysisResult.PostId,
                    Fragment = card.Original,
                    Suggestion = card.Correction,
                    ErrorType = card.Type,
                    Message = string.IsNullOrWhiteSpace(card.Explanation)
                        ? card.Hint
                        : $"{card.Explanation} {card.Hint}".Trim(),
                    Position = card.Offset,
                    IsSuspicious = card.IsSuspicious
                });
            }

            foreach (var suspicious in filterResult.SuspiciousErrors)
            {
                _context.GrammarErrors.Add(new GrammarError
                {
                    Id = Guid.NewGuid(),
                    AnalysisResultId = post.AnalysisResult.PostId,
                    Fragment = suspicious.Fragment ?? string.Empty,
                    Suggestion = suspicious.Suggestion,
                    ErrorType = suspicious.Type,
                    Message = suspicious.Message,
                    Position = suspicious.Offset,
                    IsSuspicious = true
                });
            }

            post.AnalysisResult.GrammarCheckedAt = DateTime.UtcNow;
            post.AnalysisResult.UpdatedAt = DateTime.UtcNow;
            post.Status = "Проанализирован";

            await _context.SaveChangesAsync();


            return Ok(new EnhancedGrammarResponse
            {
                RawErrors = acceptedErrors,
                Cards = cards,
                SuspiciousCards = filterResult.SuspiciousErrors.Select(x => new GrammarResultCardDto
                {
                    Original = x.Fragment ?? string.Empty,
                    Correction = x.Suggestion ?? string.Empty,
                    Type = x.Type ?? string.Empty,
                    Hint = x.Message ?? string.Empty,
                    Explanation = "Сомнительное срабатывание, требует дополнительной проверки.",
                    Offset = x.Offset,
                    Length = x.Length,
                    IsSuspicious = true,
                    Sentence = ExtractSentenceByOffset(post.Text, x.Offset)
                }).ToList()
            });
        }

        [HttpPost("{postId}")]
        public async Task<ActionResult<StyleCheckResultDto>> RunStyleCheck(Guid postId)
        {
            var post = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null)
                return NotFound();

            var result = await _llmService.StyleCheck(
                post.Community.TargetAudience ?? string.Empty,
                post.Community.StyleProfile ?? string.Empty,
                post.Text);

            if (post.AnalysisResult == null)
            {
                post.AnalysisResult = new AnalysisResult
                {
                    PostId = post.Id,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.AnalysisResults.Add(post.AnalysisResult);
            }

            post.AnalysisResult.StyleCheckedAt = DateTime.UtcNow;
            post.AnalysisResult.StyleAssessment = result.Assessment;
            post.AnalysisResult.StyleSummary = result.Summary;
            post.AnalysisResult.StyleStrengthsJson = JsonSerializer.Serialize(result.Strengths ?? new List<string>());
            post.AnalysisResult.StyleIssuesJson = JsonSerializer.Serialize(result.Issues ?? new List<string>());
            post.AnalysisResult.StyleRecommendationsJson = JsonSerializer.Serialize(result.Recommendations ?? new List<string>());
            post.AnalysisResult.UpdatedAt = DateTime.UtcNow;
            post.Status = "Проанализирован";

            await _context.SaveChangesAsync();

            return Ok(result);
        }

        [HttpPost("{postId}")]
        public async Task<ActionResult<AnalyzePostResponse>> RunRegulationCheck(Guid postId)
        {
            var post = await _context.Posts
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null)
                return NotFound();

            var resultJson = await _llmService.AnalyzePostWithRagAsync(post.Text, post.CommunityId);

            AnalyzePostResponse response;
            try
            {
                response = JsonSerializer.Deserialize<AnalyzePostResponse>(resultJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AnalyzePostResponse();
            }
            catch
            {
                response = new AnalyzePostResponse
                {
                    HasViolations = false,
                    Violations = new List<ViolationDto>(),
                    Comment = "Не удалось корректно обработать ответ модели."
                };
            }

            response.Violations ??= new List<ViolationDto>();

            if (post.AnalysisResult == null)
            {
                post.AnalysisResult = new AnalysisResult
                {
                    PostId = post.Id,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.AnalysisResults.Add(post.AnalysisResult);
            }

            var oldMatches = await _context.ProhibitedTopicMatches
                .Where(x => x.AnalysisResultId == post.AnalysisResult.PostId)
                .ToListAsync();

            _context.ProhibitedTopicMatches.RemoveRange(oldMatches);

            foreach (var violation in response.Violations)
            {
                _context.ProhibitedTopicMatches.Add(new ProhibitedTopicMatch
                {
                    Id = Guid.NewGuid(),
                    AnalysisResultId = post.AnalysisResult.PostId,
                    Topic = violation.RuleShort ?? "Нарушение",
                    Evidence = violation.MatchedText,
                    RegulationRef = $"Правило {violation.RuleNumber}",
                    Explanation = violation.Explanation
                });
            }

            post.AnalysisResult.RegulationCheckedAt = DateTime.UtcNow;
            post.AnalysisResult.HasRegulationViolations = response.HasViolations;
            post.AnalysisResult.RegulationComment = response.Comment;
            post.AnalysisResult.UpdatedAt = DateTime.UtcNow;
            post.Status = "Проанализирован";

            await _context.SaveChangesAsync();

            return Ok(response);
        }
        private static string ExtractSentenceByOffset(string text, int offset)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (offset < 0 || offset >= text.Length)
                return text.Length <= 250 ? text : text.Substring(0, 250);

            int start = offset;
            int end = offset;

            while (start > 0)
            {
                char c = text[start - 1];
                if (c == '.' || c == '!' || c == '?' || c == '\n')
                    break;
                start--;
            }

            while (end < text.Length)
            {
                char c = text[end];
                if (c == '.' || c == '!' || c == '?' || c == '\n')
                {
                    end++;
                    break;
                }
                end++;
            }

            var sentence = text.Substring(start, end - start).Trim();

            if (sentence.Length > 300)
                sentence = sentence.Substring(0, 300).Trim();

            return sentence;
        }

        // ===== ПРОГНОЗ ВОВЛЕЧЕННОСТИ =====
        [HttpPost("{postId}")]
        public async Task<ActionResult<EngagementForecastDto>> RunForecast(Guid postId)
        {
            var post = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == postId, HttpContext.RequestAborted);

            if (post == null)
                return NotFound("Пост не найден.");

            if (post.Community == null)
                return BadRequest("У поста не указано сообщество.");

            if (string.IsNullOrWhiteSpace(post.Text))
                return BadRequest("Текст поста не может быть пустым.");

            try
            {
                await _vkService.EnsureCommunityPostsSyncedAsync(communityId: post.CommunityId, maxPages: 3, minExistingPosts: 30, refreshInterval: TimeSpan.FromHours(12), ct: HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось синхронизировать историю VK перед прогнозом. PostId={PostId}, CommunityId={CommunityId}", postId, post.CommunityId);

                var existingPostsCount = await _context.CommunityPosts
                    .CountAsync(x => x.CommunityId == post.CommunityId && x.Source == "vk", HttpContext.RequestAborted);

                if (existingPostsCount == 0)
                {
                    return BadRequest("Не удалось загрузить историю публикаций VK. Проверьте ссылку на сообщество и доступность стены.");
                }
            }

            var forecast = await _llmService.ForecastEngagementAsync(post.CommunityId, post.Text, 30, HttpContext.RequestAborted);

            var result = post.AnalysisResult ?? new AnalysisResult
            {
                PostId = postId,
                UpdatedAt = DateTime.UtcNow
            };

            result.EngagementForecastJson = JsonSerializer.Serialize(forecast);
            result.ForecastCheckedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;

            post.Status = "Проанализирован";

            if (post.AnalysisResult == null)
                _context.AnalysisResults.Add(result);

            await _context.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(forecast);
        }

        // ===== РЕКОМЕНДАЦИИ ПО КОНТЕНТУ =====
        [HttpPost("{postId}")]
        public async Task<ActionResult<ContentRecommendationsDto>> RunRecommendations(Guid postId)
        {
            var post = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == postId, HttpContext.RequestAborted);

            if (post == null)
                return NotFound("Пост не найден.");

            if (post.Community == null)
                return BadRequest("У поста не указано сообщество.");

            if (string.IsNullOrWhiteSpace(post.Text))
                return BadRequest("Текст поста не может быть пустым.");

            try
            {
                await _vkService.EnsureCommunityPostsSyncedAsync(communityId: post.CommunityId, maxPages: 3, minExistingPosts: 30, refreshInterval: TimeSpan.FromHours(12), ct: HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось синхронизировать историю VK перед рекомендациями. PostId={PostId}, CommunityId={CommunityId}", postId, post.CommunityId);

                var existingPostsCount = await _context.CommunityPosts
                    .CountAsync(x => x.CommunityId == post.CommunityId && x.Source == "vk", HttpContext.RequestAborted);

                if (existingPostsCount == 0)
                {
                    return BadRequest("Не удалось загрузить историю публикаций VK. Проверьте ссылку на сообщество и доступность стены.");
                }
            }

            var recs = await _llmService.GenerateRecommendationsAsync(post.CommunityId, post.Text, 30, HttpContext.RequestAborted);

            var result = post.AnalysisResult ?? new AnalysisResult
            {
                PostId = postId,
                UpdatedAt = DateTime.UtcNow
            };

            result.RecommendationsJson = JsonSerializer.Serialize(recs);
            result.RecommendationsCheckedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;

            post.Status = "Проанализирован";

            if (post.AnalysisResult == null)
                _context.AnalysisResults.Add(result);

            await _context.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(recs);
        }

        private Guid? GetCurrentUserId()
        {
            if (!Request.Headers.TryGetValue("X-User-Id", out var values))
                return null;

            var rawUserId = values.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(rawUserId))
                return null;

            if (!Guid.TryParse(rawUserId, out var userId))
                return null;

            return userId;
        }
    }
}
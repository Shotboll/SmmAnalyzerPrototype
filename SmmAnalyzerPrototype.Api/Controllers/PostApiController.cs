using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text.Json;

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

        public PostApiController(
            AppDbContext context,
            LlmService llmService,
            LanguageToolService languageToolService,
            GrammarFalsePositiveFilterService grammarFilterService)
        {
            _context = context;
            _llmService = llmService;
            _languageToolService = languageToolService;
            _grammarFilterService = grammarFilterService;
        }

        [HttpGet]
        public async Task<ActionResult<List<PostListItemDto>>> GetAll()
        {
            var posts = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.Author)
                .Include(p => p.AnalysisResult)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var result = posts.Select(p => new PostListItemDto
            {
                Id = p.Id,
                TextPreview = p.Text.Length > 140 ? p.Text.Substring(0, 140) + "..." : p.Text,
                CommunityName = p.Community.Name,
                AuthorLogin = p.Author.Login,
                CreatedAt = p.CreatedAt,
                Status = p.Status,
                GrammarChecked = p.AnalysisResult?.GrammarCheckedAt != null,
                StyleChecked = p.AnalysisResult?.StyleCheckedAt != null,
                RegulationChecked = p.AnalysisResult?.RegulationCheckedAt != null
            }).ToList();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PostDetailsDto>> GetById(Guid id)
        {
            var post = await _context.Posts
                .Include(p => p.Community)
                .Include(p => p.Author)
                .Include(p => p.AnalysisResult)
                    .ThenInclude(a => a.GrammarErrors)
                .Include(p => p.AnalysisResult)
                    .ThenInclude(a => a.ProhibitedTopicMatches)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return NotFound();

            var dto = new PostDetailsDto
            {
                Id = post.Id,
                Text = post.Text,
                CommunityId = post.CommunityId,
                CommunityName = post.Community.Name,
                AuthorLogin = post.Author.Login,
                CreatedAt = post.CreatedAt,
                Status = post.Status,
                GrammarCheckedAt = post.AnalysisResult?.GrammarCheckedAt,
                StyleCheckedAt = post.AnalysisResult?.StyleCheckedAt,
                RegulationCheckedAt = post.AnalysisResult?.RegulationCheckedAt,
                StyleAssessment = post.AnalysisResult?.StyleAssessment,
                StyleSummary = post.AnalysisResult?.StyleSummary,
                HasRegulationViolations = post.AnalysisResult?.HasRegulationViolations,
                RegulationComment = post.AnalysisResult?.RegulationComment
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

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PostDetailsDto>> Create([FromBody] CreatePostRequest request)
        {
            if (request == null || request.CommunityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
                return BadRequest();

            var firstUser = await _context.Users.FirstOrDefaultAsync();
            if (firstUser == null)
                return BadRequest("В системе нет пользователя.");

            var post = new Post
            {
                Id = Guid.NewGuid(),
                Text = request.Text.Trim(),
                CommunityId = request.CommunityId,
                AuthorId = firstUser.Id,
                CreatedAt = DateTime.UtcNow,
                Status = "Draft"
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
                CreatedAt = post.CreatedAt,
                Status = post.Status,
                AuthorLogin = firstUser.Login
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePostRequest request)
        {
            var post = await _context.Posts
                .Include(p => p.AnalysisResult)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return NotFound();

            var textChanged = post.Text.Trim() != request.Text.Trim();
            var communityChanged = post.CommunityId != request.CommunityId;

            post.Text = request.Text.Trim();
            post.CommunityId = request.CommunityId;
            post.UpdatedAt = DateTime.UtcNow;

            if (textChanged || communityChanged)
            {
                post.Status = "Draft";

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

                    post.AnalysisResult.StyleAssessment = null;
                    post.AnalysisResult.StyleSummary = null;
                    post.AnalysisResult.StyleStrengthsJson = null;
                    post.AnalysisResult.StyleIssuesJson = null;
                    post.AnalysisResult.StyleRecommendationsJson = null;

                    post.AnalysisResult.HasRegulationViolations = null;
                    post.AnalysisResult.RegulationComment = null;
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
            post.Status = "Analyzed";

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
            post.Status = "Analyzed";

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
            post.Status = "Analyzed";

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
    }
}
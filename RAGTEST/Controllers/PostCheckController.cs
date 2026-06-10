using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGTEST.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Security.Claims;
using System.Text.Json;

namespace RAGTEST.Controllers
{
    [Authorize]
    public class PostCheckController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PostCheckController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient CreateApiClient()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
                client.DefaultRequestHeaders.Add("X-User-Id", userId);

            return client;
        }

        [HttpGet]
        public async Task<IActionResult> Grammar(Guid postId)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
            if (post == null)
                return NotFound();

            var model = new GrammarCheckPageModel
            {
                PostId = post.Id,
                CommunityId = post.CommunityId,
                CommunityName = post.CommunityName,
                Text = post.Text
            };

            if (post.GrammarCheckedAt != null && post.GrammarErrors.Any())
            {
                model.HasResult = true;

                model.Cards = post.GrammarErrors
                    .Where(x => !x.IsSuspicious)
                    .Select(x => new GrammarResultCardDto
                    {
                        Original = x.Fragment,
                        Correction = x.Suggestion,
                        Type = x.Type,
                        Hint = x.Message,
                        Explanation = string.Empty,
                        Offset = x.Offset,
                        Length = x.Length,
                        IsSuspicious = false,
                        Sentence = x.Sentence
                    })
                    .ToList();

                model.SuspiciousCards = post.GrammarErrors
                    .Where(x => x.IsSuspicious)
                    .Select(x => new GrammarResultCardDto
                    {
                        Original = x.Fragment,
                        Correction = x.Suggestion,
                        Type = x.Type,
                        Hint = x.Message,
                        Explanation = string.Empty,
                        Offset = x.Offset,
                        Length = x.Length,
                        IsSuspicious = true,
                        Sentence = x.Sentence
                    })
                    .ToList();
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ExplainGrammarItemProxy([FromBody] ExplainGrammarItemRequest request)
        {
            var client = CreateApiClient();

            var response = await client.PostAsJsonAsync("api/postapi/explaingrammaritem", request);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grammar(GrammarCheckPageModel model)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null)
                return NotFound();

            model.CommunityId = post.CommunityId;
            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await client.PostAsync($"api/postapi/rungrammarcheck/{model.PostId}", null);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Ошибка при проверке грамматики.";
                return View(model);
            }

            var result = await response.Content.ReadFromJsonAsync<EnhancedGrammarResponse>();

            model.HasResult = true;
            model.Cards = result?.Cards ?? new List<GrammarResultCardDto>();
            model.SuspiciousCards = result?.SuspiciousCards ?? new List<GrammarResultCardDto>();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Style(Guid postId)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
            if (post == null)
                return NotFound();

            var model = new StyleCheckPageModel
            {
                PostId = post.Id,
                CommunityId = post.CommunityId,
                CommunityName = post.CommunityName,
                Text = post.Text
            };

            if (post.StyleCheckedAt != null)
            {
                model.Result = new StyleCheckResultDto
                {
                    Assessment = post.StyleAssessment ?? string.Empty,
                    Summary = post.StyleSummary ?? string.Empty,
                    Strengths = post.StyleStrengths ?? new List<string>(),
                    Issues = post.StyleIssues ?? new List<string>(),
                    Recommendations = post.StyleRecommendations ?? new List<string>()
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Style(StyleCheckPageModel model)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null)
                return NotFound();

            model.CommunityId = post.CommunityId;
            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await client.PostAsync($"api/postapi/runstylecheck/{model.PostId}", null);

            if (!response.IsSuccessStatusCode)
            {
                model.ErrorMessage = "Ошибка при проверке стиля.";
                return View(model);
            }

            var result = await response.Content.ReadFromJsonAsync<StyleCheckResultDto>();

            if (result == null)
            {
                model.ErrorMessage = "Не удалось обработать результат анализа.";
                return View(model);
            }

            model.Result = result;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Regulations(Guid postId)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
            if (post == null)
                return NotFound();

            var model = new RegulationCheckPageModel
            {
                PostId = post.Id,
                CommunityId = post.CommunityId,
                CommunityName = post.CommunityName,
                Text = post.Text,
                HasResult = post.RegulationCheckedAt != null,
                HasViolations = post.HasRegulationViolations ?? false,
                Comment = post.RegulationComment ?? string.Empty,
                Violations = post.RegulationViolations ?? new List<ViolationDto>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regulations(RegulationCheckPageModel model)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null)
                return NotFound();

            model.CommunityId = post.CommunityId;
            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await client.PostAsync($"api/postapi/runregulationcheck/{model.PostId}", null);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Ошибка при проверке по регламентам.";
                return View(model);
            }

            var result = await response.Content.ReadFromJsonAsync<AnalyzePostResponse>();

            model.HasResult = true;
            model.HasViolations = result?.HasViolations ?? false;
            model.Comment = result?.Comment ?? string.Empty;
            model.Violations = result?.Violations ?? new List<ViolationDto>();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Forecast(Guid postId)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
            if (post == null)
                return NotFound();

            var model = new ForecastPageModel
            {
                PostId = post.Id,
                CommunityName = post.CommunityName,
                Text = post.Text,
                ForecastCheckedAt = post.ForecastCheckedAt
            };

            if (post.ForecastCheckedAt != null)
            {
                model.Result = new EngagementForecastDto
                {
                    Level = post.ForecastLevel ?? string.Empty,
                    QualityScore = post.ForecastQualityScore,
                    ExpectedLikes = post.ForecastLikes,
                    ExpectedComments = post.ForecastComments,
                    ExpectedViews = post.ForecastViews,
                    Confidence = post.ForecastConfidence,
                    ExpectedERPercent = post.ForecastERPercent,
                    Reasoning = post.ForecastReasoning ?? string.Empty,
                    KeyFactors = post.ForecastKeyFactors ?? new List<string>(),
                    Risks = post.ForecastRisks ?? new List<string>(),
                    ComparisonWithAvg = post.ForecastComparison ?? string.Empty
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Forecast(ForecastPageModel model)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null) return NotFound();

            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await client.PostAsync($"api/postapi/runforecast/{model.PostId}", null);
            if (response.IsSuccessStatusCode)
            {
                model.Result = await response.Content.ReadFromJsonAsync<EngagementForecastDto>();
                model.ForecastCheckedAt = DateTime.UtcNow;
            }
            else
            {
                model.ErrorMessage = "Ошибка при генерации прогноза.";
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Recommendations(Guid postId)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
            if (post == null)
                return NotFound();

            var model = new RecommendationsPageModel
            {
                PostId = post.Id,
                CommunityName = post.CommunityName,
                Text = post.Text,
                RecommendationsCheckedAt = post.RecommendationsCheckedAt
            };

            if (post.RecommendationsCheckedAt != null)
            {
                model.Result = new ContentRecommendationsDto
                {
                    TextImprovements = post.RecommendationsText ?? new List<string>(),
                    StructuralChanges = post.RecommendationsStruct ?? new List<string>(),
                    TopicIdeas = post.RecommendationsTopics ?? new List<string>(),
                    EngagementBoosters = post.RecommendationsBoosters ?? new List<string>(),
                    OverallAdvice = post.RecommendationsAdvice ?? string.Empty
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recommendations(RecommendationsPageModel model)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null) return NotFound();

            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await client.PostAsync($"api/postapi/runrecommendations/{model.PostId}", null);
            if (response.IsSuccessStatusCode)
            {
                model.Result = await response.Content.ReadFromJsonAsync<ContentRecommendationsDto>();
                model.RecommendationsCheckedAt = DateTime.UtcNow;
            }
            else
            {
                model.ErrorMessage = "Ошибка при генерации рекомендаций.";
            }
            return View(model);
        }
    }
}
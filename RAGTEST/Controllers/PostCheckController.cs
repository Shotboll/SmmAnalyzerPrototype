using Microsoft.AspNetCore.Mvc;
using RAGTEST.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Controllers
{
    public class PostCheckController : Controller
    {
        private readonly HttpClient _client;

        public PostCheckController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("Api");
        }

        [HttpGet]
        public async Task<IActionResult> Grammar(Guid postId)
        {
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
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
            var response = await _client.PostAsJsonAsync("api/postapi/explaingrammaritem", request);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grammar(GrammarCheckPageModel model)
        {
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null)
                return NotFound();

            model.CommunityId = post.CommunityId;
            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await _client.PostAsync($"api/postapi/rungrammarcheck/{model.PostId}", null);

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
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
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
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null)
                return NotFound();

            model.CommunityId = post.CommunityId;
            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await _client.PostAsync($"api/postapi/runstylecheck/{model.PostId}", null);

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
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{postId}");
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
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{model.PostId}");
            if (post == null)
                return NotFound();

            model.CommunityId = post.CommunityId;
            model.CommunityName = post.CommunityName;
            model.Text = post.Text;

            var response = await _client.PostAsync($"api/postapi/runregulationcheck/{model.PostId}", null);

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
    }
}
using Microsoft.AspNetCore.Mvc;
using RAGTEST.Models;
using SmmAnalyzerPrototype.Data.Models.DTO;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RAGTEST.Controllers
{
    public class PostController : Controller
    {
        private readonly HttpClient _client;

        public PostController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("Api");
        }

        public async Task<IActionResult> Create()
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            var model = new PostCreateModel
            {
                Communities = communities
            };
            return View(model);
        }

        // GET: /Post/RegulationCheck
        public async Task<IActionResult> RegulationCheck()
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            var model = new RegulationCheckPageModel
            {
                Communities = communities
            };

            return View(model);
        }

        // POST: /Post/RegulationCheck
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegulationCheck(RegulationCheckPageModel model)
        {
            model.Communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            if (model.CommunityId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(model.CommunityId), "Выберите сообщество.");
            }

            if (string.IsNullOrWhiteSpace(model.Text))
            {
                ModelState.AddModelError(nameof(model.Text), "Введите текст поста.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var request = new AnalyzePostRequest
            {
                CommunityId = model.CommunityId,
                Text = model.Text
            };

            var response = await _client.PostAsJsonAsync("api/postapi/AnalyzePost", request);

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

        // POST: /Post/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostCreateModel model)
        {
            model.Communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            if (model.CommunityId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(model.CommunityId), "Выберите сообщество.");
            }

            if (string.IsNullOrWhiteSpace(model.Text))
            {
                ModelState.AddModelError(nameof(model.Text), "Введите текст поста.");
            }

            if (!string.IsNullOrWhiteSpace(model.Text) && model.Text.Length > 5000)
            {
                ModelState.AddModelError(nameof(model.Text), "Текст не должен превышать 5000 символов.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var analyzeRequest = new AnalyzePostRequest
            {
                Text = model.Text,
                CommunityId = model.CommunityId
            };

            try
            {
                var grammarResponse = await _client.PostAsJsonAsync("api/postapi/CheckGrammar", analyzeRequest);

                if (!grammarResponse.IsSuccessStatusCode)
                {
                    ViewBag.Error = "Ошибка при проверке грамматики.";
                    return View(model);
                }

                var grammarResult = await grammarResponse.Content.ReadFromJsonAsync<EnhancedGrammarResponse>();

                TempData["GrammarCards"] = JsonSerializer.Serialize(grammarResult?.Cards ?? new List<GrammarResultCardDto>());
                TempData["SuspiciousGrammarCards"] = JsonSerializer.Serialize(grammarResult?.SuspiciousCards ?? new List<GrammarResultCardDto>());

                return RedirectToAction("Analysis");
            }
            catch (TaskCanceledException)
            {
                ViewBag.Error = "Превышено время ожидания ответа от модели.";
                return View(model);
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Не удалось подключиться к сервису анализа.";
                return View(model);
            }
            catch (Exception)
            {
                ViewBag.Error = "Произошла непредвиденная ошибка при анализе текста.";
                return View(model);
            }
        }

        // GET: /Post/Analysis
        public IActionResult Analysis()
        {
            var cardsJson = TempData["GrammarCards"]?.ToString();
            var suspiciousJson = TempData["SuspiciousGrammarCards"]?.ToString();

            var model = new GrammarAnalysisPageModel
            {
                Cards = !string.IsNullOrWhiteSpace(cardsJson)
                    ? JsonSerializer.Deserialize<List<GrammarResultCardDto>>(cardsJson) ?? new List<GrammarResultCardDto>()
                    : new List<GrammarResultCardDto>(),

                SuspiciousCards = !string.IsNullOrWhiteSpace(suspiciousJson)
                    ? JsonSerializer.Deserialize<List<GrammarResultCardDto>>(suspiciousJson) ?? new List<GrammarResultCardDto>()
                    : new List<GrammarResultCardDto>()
            };

            return View(model);
        }

        // GET: /Post/StyleCheck
        [HttpGet]
        public async Task<IActionResult> StyleCheck()
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            var model = new StyleCheckPageModel
            {
                Communities = communities
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> StyleCheck(StyleCheckPageModel model)
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();
            model.Communities = communities;

            if (!ModelState.IsValid)
                return View(model);

            var community = communities.FirstOrDefault(c => c.Id == model.CommunityId);
            if (community == null)
            {
                model.ErrorMessage = "Выбранное сообщество не найдено.";
                return View(model);
            }

            var request = new StyleCheckRequest
            {
                Audience = community.TargetAudience ?? string.Empty,
                Style = community.StyleProfile ?? string.Empty,
                Text = model.Text
            };

            var response = await _client.PostAsJsonAsync("api/postapi/CheckStyle", request);

            if (!response.IsSuccessStatusCode)
            {
                model.ErrorMessage = "Ошибка при проверке стиля.";
                return View(model);
            }

            var rawJson = await response.Content.ReadAsStringAsync();

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<StyleCheckResultDto>(rawJson, options);
                if (result == null)
                {
                    model.ErrorMessage = "Не удалось обработать результат анализа.";
                    return View(model);
                }

                model.Result = result;
            }
            catch
            {
                model.ErrorMessage = "Модель вернула некорректный ответ.";
            }

            return View(model);
        }
    }
}
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

        // POST: /Post/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostCreateModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();
                return View(model);
            }

            var analyzeRequest = new AnalyzePostRequest
            {
                Text = model.Text,
                CommunityId = model.CommunityId
            };

            var grammarResponse = await _client.PostAsJsonAsync("api/postapi/CheckGrammar", analyzeRequest);

            if (!grammarResponse.IsSuccessStatusCode)
            {
                model.Communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();
                ViewBag.Error = "Ошибка при проверке грамматики.";
                return View(model);
            }

            var grammarResult = await grammarResponse.Content.ReadFromJsonAsync<EnhancedGrammarResponse>();

            TempData["GrammarCards"] = JsonSerializer.Serialize(grammarResult?.Cards ?? new List<GrammarResultCardDto>());
            TempData["SuspiciousGrammarCards"] = JsonSerializer.Serialize(grammarResult?.SuspiciousCards ?? new List<GrammarResultCardDto>());

            return RedirectToAction("Analysis");
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
        public async Task<IActionResult> StyleCheck()
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            var model = new PostCreateModel
            {
                Communities = communities
            };
            return View(model);
        }

        // POST: /Post/StyleCheck
        [HttpPost]
        public async Task<IActionResult> StyleCheck(PostCreateModel model)
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new();

            var community = communities.Where(a => a.Id == model.CommunityId).First();

            var request = new StyleCheckRequest()
            {
                Audience = community.TargetAudience!,
                Style = community.StyleProfile!,
                Text = model.Text
            };

            model.Communities = communities;

            var response = await _client.PostAsJsonAsync("api/postapi/CheckStyle", request);
            if (response.IsSuccessStatusCode)
            {
                ViewBag.StyleResult = response.Content.ReadAsStringAsync().Result;
            }
            else
            {
                ViewBag.Error = "Ошибка при проверке стиля";
            }
            return View(model);
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using SmmAnalyzerPrototype.Data;
using SmmAnalyzerPrototype.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.AI;
using OllamaSharp;

namespace SmmAnalyzerPrototype.Controllers
{
    public class PostController : Controller
    {
        private readonly LlmService _llmService;
        private static readonly List<float[]> _embeddings = new();

        public PostController(LlmService llmService)
        {
            _llmService = llmService;
        }

        public IActionResult Create()
        {
            // Загружаем список сообществ для выпадающего списка
            var communities = new List<Community>
        {
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "IT-новости" },
            new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Образование" }
        };

            ViewBag.Communities = communities.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(PostCreateModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Text))
            {
                ViewBag.Error = "Введите текст поста.";
                return View();
            }

            var result = await _llmService.AnalyzePostAsync(model.Text);
            ViewBag.Result = result;

            return View();
        }

        //[HttpPost]
        //public IActionResult Create(PostCreateModel model)
        //{
        //    if (!ModelState.IsValid) return View(model);

        //    // Эмуляция анализа
        //    var analysis = new AnalysisResult
        //    {
        //        GrammarErrors = new List<ErrorDetail>
        //{
        //    new() { Type = "пунктуация", Fragment = "привет как дела", Suggestion = "Привет, как дела?", Position = 0 },
        //    new() { Type = "речевая ошибка", Fragment = "самый уникальнейший", Suggestion = "уникальный", Position = 42 },
        //    new() { Type = "стилевая избыточность", Fragment = "абсолютно точно", Suggestion = "точно", Position = 67 }
        //},
        //        StyleAssessment = "Стиль соответствует аудитории",
        //        ProhibitedTopics = new(),
        //        EngagementForecast = "Выше среднего (+15%)",
        //        Recommendations = new List<TopicRecommendation>
        //{
        //    new() { Topic = "Цифровая грамотность", Reason = "Часто обсуждается в комментариях" }
        //}
        //    };

        //    TempData["Analysis"] = JsonSerializer.Serialize(analysis, JsonOptions);
        //    return RedirectToAction("Analysis");
        //}

        public IActionResult Analysis()
        {
            var json = TempData["Analysis"]?.ToString();
            var analysis = json != null
                ? JsonSerializer.Deserialize<AnalysisResult>(json, JsonOptions)
                : null;

            if (analysis == null)
                return RedirectToAction("Create");

            return View(analysis);
        }

        public async Task<IActionResult> History()
        {
            //var posts = new List<Post>
            //{
            //    new()
            //    {
            //        Id = Guid.NewGuid(),
            //        Text = "Как выучить Python за 30 дней? Советы от экспертов",
            //        CreatedAt = DateTime.Now.AddDays(-2),
            //        Status = PostStatus.Approved,
            //        CommunityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            //        AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000002")
            //    },
            //    new()
            //    {
            //        Id = Guid.NewGuid(),
            //        Text = "Топ-5 инструментов для автоматизации рутины в 2025",
            //        CreatedAt = DateTime.Now.AddDays(-5),
            //        Status = PostStatus.Approved,
            //        CommunityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            //        AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000002")
            //    },
            //    new()
            //    {
            //        Id = Guid.NewGuid(),
            //        Text = "Почему не стоит верить всем советам из TikTok",
            //        CreatedAt = DateTime.Now.AddDays(-8),
            //        Status = PostStatus.Rejected,
            //        CommunityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            //        AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000002")
            //    }
            //};
            //return View(posts);

            var ollama = new OllamaApiClient(new Uri("http://localhost:11434/"));
            ollama.SelectedModel = "bge-m3"; // модель для эмбеддингов

            var embedding = await ollama.EmbedAsync("Запрещено упоминание политики");
            float[] vector = embedding.Embeddings[0];
            _embeddings.Add(vector);

            return View();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

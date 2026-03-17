using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.EntityFrameworkCore;
using OllamaSharp;
using Pgvector;
using RAGTEST.Data;
using RAGTEST.Models;
using RAGTEST.Services;
using System.Threading.Tasks;

namespace RAGTEST.Controllers
{
    public class RagTestController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly LlmService _llmService;
        private readonly VkService _vkService;

        public RagTestController(AppDbContext context, IEmbeddingService embeddingService, LlmService llmService, VkService vkService)
        {
            _context = context;
            _embeddingService = embeddingService;
            _llmService = llmService;
            _vkService = vkService;
        }

        public IActionResult Index()
        {
            return View();
        }

        //-------------------------RAGTEST----------------------------
        private float[] NormalizeVector(float[] vector)
        {
            float norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (norm == 0) return vector;
            return vector.Select(x => x / norm).ToArray();
        }

        [HttpPost]
        public async Task<IActionResult> AddChunk(string chunkText, Guid communityId, string metadata)
        {
            if (string.IsNullOrWhiteSpace(chunkText))
            {
                ViewBag.Error = "Текст чанка не может быть пустым";
                ViewBag.Metadata = metadata;
                return View("Index");
            }

            try
            {
                float[] embeddingArray = await _embeddingService.GetEmbeddingAsync(chunkText, isQuery: false);
                float[] normalizedVector = NormalizeVector(embeddingArray);

                var pgVector = new Vector(normalizedVector);

                var chunk = new RegulationChunk
                {
                    RegulationId = Guid.NewGuid(),
                    ChunkText = chunkText,
                    ChunkIndex = 0,
                    Embedding = pgVector,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RegulationChunks.Add(chunk);
                await _context.SaveChangesAsync();

                ViewBag.Success = "Чанк успешно добавлен!";
                ViewBag.Metadata = metadata;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка: {ex.Message}";
                ViewBag.Metadata = metadata;
            }

            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Search(string queryText, Guid regulationId, int topK = 5)
        {
            double threshold = 0.2;

            if (Request.Form.TryGetValue("threshold", out var thresholdString))
            {
                if (!double.TryParse(thresholdString, System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out threshold))
                {
                    double.TryParse(thresholdString, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.CurrentCulture, out threshold);
                }
            }

            try
            {
                float[] queryEmbedding = await _embeddingService.GetEmbeddingAsync(queryText, isQuery: true);
                float[] normalizedQuery = NormalizeVector(queryEmbedding);

                var allChunks = await _context.RegulationChunks
                    .Where(c => c.RegulationId == regulationId)
                    .ToListAsync();

                var results = new List<(RegulationChunk Chunk, double Distance)>();

                foreach (var chunk in allChunks)
                {
                    if (chunk.Embedding == null) continue;

                    float[] chunkArray = chunk.Embedding.ToArray();

                    double dot = 0;
                    for (int i = 0; i < normalizedQuery.Length; i++)
                    {
                        dot += normalizedQuery[i] * chunkArray[i];
                    }
                    double distance = 1.0 - dot;

                    if (distance <= threshold)
                    {
                        results.Add((chunk, distance));
                    }
                }

                var topResults = results.OrderBy(x => x.Distance).Take(topK).ToList();

                ViewBag.Results = topResults.Select(x => x.Chunk).ToList();
                ViewBag.Distances = topResults.ToDictionary(x => x.Chunk.Id, x => x.Distance);
                ViewBag.QueryText = queryText;
                ViewBag.Threshold = threshold;
                ViewBag.TopK = topK;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка при поиске: {ex.Message}";
            }
            return View("Index");
        }

        //-------------------------RAGTEST----------------------------

        public IActionResult SaigaRag()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CheckText(string text, Guid communityId)
        {
            ViewBag.Text = text;
            ViewBag.CommunityId = communityId;

            if (string.IsNullOrWhiteSpace(text))
            {
                ViewBag.Error = "Введите текст для проверки";
                return View("SaigaRag");
            }

            try
            {

                var result = await _llmService.AnalyzePostWithRagAsync(text, communityId, 3);
                ViewBag.Result = result;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка при анализе: {ex.Message}";
            }

            return View("SaigaRag");
        }

        public IActionResult TextErrors()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CheckErrorText(string text, Guid communityId)
        {
            ViewBag.Text = text;
            ViewBag.CommunityId = communityId;

            if (string.IsNullOrWhiteSpace(text))
            {
                ViewBag.Error = "Введите текст для проверки";
                return View("TextErrors");
            }

            try
            {

                var result = await _llmService.CheckErrorText(text, communityId);
                ViewBag.Result = result;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка при анализе: {ex.Message}";
            }

            return View("TextErrors");
        }

        public IActionResult StyleCheck()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StyleCheck(string audience, string style, string text)
        {
            ViewBag.Audience = audience;
            ViewBag.Style = style;
            ViewBag.Text = text;

            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(style))
            {
                ViewBag.Error = "Заполните все поля";
                return View("StyleCheck");
            }

            try
            {
                var result = await _llmService.StyleCheck(audience,style, text);
                ViewBag.StyleResult = result;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка: {ex.Message}";
            }

            return View("StyleCheck");
        }

        public async Task<IActionResult> Posts()
        {

            var result = await _vkService.GetLatestPostsAsync();

            ViewBag.GroupId = 11069256;

            return View(result);
        }
    }
}

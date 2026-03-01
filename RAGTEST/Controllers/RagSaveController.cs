using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OllamaSharp;
using Pgvector;
using RAGTEST.Data;
using RAGTEST.Models;

namespace RAGTEST.Controllers
{
    public class RagSaveController : Controller
    {
        private readonly AppDbContext _context;
        private readonly OllamaApiClient _ollama;

        public RagSaveController(AppDbContext context)
        {
            _context = context;
            _ollama = new OllamaApiClient(new Uri("http://localhost:11434/"));
            _ollama.SelectedModel = "bge-m3";
        }

        float[] NormalizeVector(float[] vector)
        {
            float norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            return vector.Select(x => x / norm).ToArray();
        }

        public IActionResult Index()
        {
            return View();
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
                var embeddingResponse = await _ollama.EmbedAsync("passage: " + chunkText);
                float[] rawVector = embeddingResponse.Embeddings[0];
                float[] normalizedVector = NormalizeVector(rawVector);

                var pgVector = new Vector(normalizedVector);

                var chunk = new RegulationChunk
                {
                    CommunityId = communityId,
                    RegulationId = Guid.NewGuid(),
                    ChunkText = chunkText,
                    ChunkIndex = 0,
                    Embedding = pgVector,
                    MetadataJson = metadata,
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
        public async Task<IActionResult> Search(string queryText, Guid communityId, int topK = 5)
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
                var queryEmbeddingResponse = await _ollama.EmbedAsync("query: " + queryText);
                float[] queryArray = queryEmbeddingResponse.Embeddings[0];
                queryArray = NormalizeVector(queryArray);

                var allChunks = await _context.RegulationChunks
                    .Where(c => c.CommunityId == communityId)
                    .ToListAsync();

                var results = new List<(RegulationChunk Chunk, double Distance)>();

                foreach (var chunk in allChunks)
                {
                    if (chunk.Embedding == null) continue;

                    float[] chunkArray = chunk.Embedding.ToArray();

                    double dot = 0, normQuery = 0, normChunk = 0;
                    for (int i = 0; i < queryArray.Length; i++)
                    {
                        dot += queryArray[i] * chunkArray[i];
                        normQuery += queryArray[i] * queryArray[i];
                        normChunk += chunkArray[i] * chunkArray[i];
                    }
                    double distance = 1.0 - dot / (Math.Sqrt(normQuery) * Math.Sqrt(normChunk));

                    if (distance <= threshold)
                    {
                        results.Add((chunk, distance));
                    }
                }

                var topResults = results.OrderBy(x => x.Distance).Take(topK).ToList();

                ViewBag.Results = topResults.Select(x => x.Chunk).ToList();
                ViewBag.Distances = topResults.ToDictionary(x => x.Chunk.Id, x => x.Distance);
                ViewBag.QueryText = queryText;
                ViewBag.CommunityId = communityId;
                ViewBag.Threshold = threshold;
                ViewBag.TopK = topK;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка при поиске: {ex.Message}";
            }
            return View("Index");
        }
    }
}

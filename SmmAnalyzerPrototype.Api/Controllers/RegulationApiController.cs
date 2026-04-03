using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Regualtion;
using System.Text.RegularExpressions;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class RegulationApiController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmbeddingService _embeddingService;

        public RegulationApiController(AppDbContext context, IEmbeddingService embeddingService)
        {
            _context = context;
            _embeddingService = embeddingService;
        }

        [HttpGet]
        public async Task<ActionResult<List<RegulationDocumentDto>>> GetAll([FromQuery] Guid? communityId)
        {
            var query = _context.RegulationDocuments.AsQueryable();
            if (communityId.HasValue)
                query = query.Where(r => r.CommunityId == communityId.Value);

            var docs = await query.ToListAsync();
            return Ok(docs.Select(MapToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RegulationDocumentDto>> GetById(Guid id)
        {
            var doc = await _context.RegulationDocuments
                .Include(r => r.Chunks)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (doc == null) return NotFound();
            return Ok(MapToDto(doc));
        }

        [HttpPost]
        public async Task<ActionResult<RegulationDocumentDto>> Create([FromBody] CreateRegulationRequest request)
        {
            var doc = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Content = request.Content,
                Category = request.Category,
                CommunityId = request.CommunityId
            };

            // Разбиваем на чанки и получаем эмбеддинги
            var chunks = SplitTextIntoChunks(request.Content);
            foreach (var (chunkText, index) in chunks)
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(chunkText, isQuery: false);
                var chunk = new RegulationChunk
                {
                    Id = Guid.NewGuid(),
                    RegulationId = doc.Id,
                    ChunkText = chunkText,
                    ChunkIndex = index,
                    Embedding = new Vector(embedding),
                    CreatedAt = DateTime.UtcNow
                };
                doc.Chunks.Add(chunk);
            }

            _context.RegulationDocuments.Add(doc);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = doc.Id }, MapToDto(doc));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRegulationRequest request)
        {
            var doc = await _context.RegulationDocuments
                .Include(r => r.Chunks)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (doc == null) return NotFound();

            doc.Title = request.Title;
            doc.Content = request.Content;
            doc.Category = request.Category;

            // Удаляем старые чанки
            _context.RegulationChunks.RemoveRange(doc.Chunks);

            // Создаём новые чанки
            var chunks = SplitTextIntoChunks(request.Content);
            foreach (var (chunkText, index) in chunks)
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(chunkText, isQuery: false);
                var chunk = new RegulationChunk
                {
                    Id = Guid.NewGuid(),
                    RegulationId = doc.Id,
                    ChunkText = chunkText,
                    ChunkIndex = index,
                    Embedding = new Vector(embedding),
                    CreatedAt = DateTime.UtcNow
                };
                _context.RegulationChunks.Add(chunk);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var doc = await _context.RegulationDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            _context.RegulationDocuments.Remove(doc);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private List<(string, int)> SplitTextIntoChunks(string text, int chunkSize = 1000)
        {
            var chunks = new List<(string, int)>();
            int index = 0;

            // 1. Разбиваем на абзацы (по пустым строкам)
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                var trimmedParagraph = paragraph.Trim();
                if (string.IsNullOrWhiteSpace(trimmedParagraph)) continue;

                // Если абзац помещается в чанк целиком
                if (trimmedParagraph.Length <= chunkSize)
                {
                    chunks.Add((trimmedParagraph, index++));
                    continue;
                }

                // 2. Длинный абзац — разбиваем по предложениям
                var sentences = Regex.Split(trimmedParagraph, @"(?<=[.!?])\s+")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                var currentChunk = new List<string>();
                int currentLength = 0;

                foreach (var sentence in sentences)
                {
                    var trimmedSentence = sentence.Trim();
                    if (trimmedSentence.Length > chunkSize)
                    {
                        // Очень длинное предложение – принудительно разбиваем по словам
                        var words = trimmedSentence.Split(' ');
                        var tempChunk = new List<string>();
                        int tempLength = 0;
                        foreach (var word in words)
                        {
                            if (tempLength + word.Length + 1 > chunkSize && tempChunk.Any())
                            {
                                chunks.Add((string.Join(" ", tempChunk), index++));
                                tempChunk.Clear();
                                tempLength = 0;
                            }
                            tempChunk.Add(word);
                            tempLength += word.Length + 1;
                        }
                        if (tempChunk.Any())
                            chunks.Add((string.Join(" ", tempChunk), index++));
                        continue;
                    }

                    if (currentLength + trimmedSentence.Length > chunkSize && currentChunk.Any())
                    {
                        chunks.Add((string.Join(" ", currentChunk), index++));
                        currentChunk.Clear();
                        currentLength = 0;
                    }
                    currentChunk.Add(trimmedSentence);
                    currentLength += trimmedSentence.Length;
                }

                if (currentChunk.Any())
                    chunks.Add((string.Join(" ", currentChunk), index++));
            }

            return chunks;
        }

        private RegulationDocumentDto MapToDto(RegulationDocument doc)
        {
            var dto = new RegulationDocumentDto
            {
                Id = doc.Id,
                Title = doc.Title,
                Content = doc.Content,
                Category = doc.Category,
                CommunityId = doc.CommunityId
            };
            if (doc.Chunks != null)
            {
                dto.Chunks = doc.Chunks.Select(c => new RegulationChunkDto
                {
                    Id = c.Id,
                    Text = c.ChunkText,
                    Index = c.ChunkIndex,
                    CreatedAt = c.CreatedAt
                }).ToList();
            }
            return dto;
        }
    }
}

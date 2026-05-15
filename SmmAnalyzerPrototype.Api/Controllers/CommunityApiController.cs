using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using VkNet.Model;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class CommunityApiController : Controller
    {
        private readonly AppDbContext _context;
        private readonly VkService _vkService;
        private readonly ILogger<CommunityApiController> _logger;

        public CommunityApiController(AppDbContext context, VkService vkService, ILogger<CommunityApiController> logger)
        {
            _context = context;
            _vkService = vkService;
            _logger = logger;
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

        [HttpGet]
        public async Task<ActionResult<List<CommunityDto>>> GetAll()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            var communities = await _context.Communities
                .AsNoTracking()
                .Where(c => c.UserId == userId.Value)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(communities.Select(MapToDto).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommunityDto>> GetById(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            var community = await _context.Communities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

            if (community == null)
                return NotFound("Сообщество не найдено.");

            return Ok(MapToDto(community));
        }

        [HttpPost]
        public async Task<ActionResult<CommunityDto>> Create([FromBody] CreateCommunityRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            if (request == null)
                return BadRequest("Некорректные данные сообщества.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Название сообщества не может быть пустым.");

            var community = new Community
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                TargetAudience = request.TargetAudience?.Trim() ?? string.Empty,
                StyleProfile = request.StyleProfile?.Trim() ?? string.Empty,
                VkInput = request.VkInput?.Trim(),
                UserId = userId.Value
            };

            if (!string.IsNullOrWhiteSpace(request.VkInput))
            {
                try
                {
                    var vkInfo = await _vkService.ResolveCommunityAsync(
                        request.VkInput,
                        HttpContext.RequestAborted);

                    if (vkInfo == null)
                    {
                        return BadRequest("Не удалось определить VK-сообщество по указанной ссылке или ID.");
                    }

                    community.VkGroupId = vkInfo.GroupId;
                    community.VkScreenName = vkInfo.ScreenName;
                    community.VkUrl = vkInfo.Url;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Ошибка при распознавании VK-сообщества. VkInput={VkInput}",
                        request.VkInput);

                    return BadRequest("Ошибка при проверке VK-сообщества. Проверьте ссылку или ID сообщества.");
                }
            }

            _context.Communities.Add(community);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = community.Id }, MapToDto(community));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommunityRequest request)
        {

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            if (request == null)
                return BadRequest("Некорректные данные сообщества.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Название сообщества не может быть пустым.");

            var community = await _context.Communities.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

            if (community == null)
                return NotFound("Сообщество не найдено.");

            community.Name = request.Name.Trim();
            community.TargetAudience = request.TargetAudience?.Trim() ?? string.Empty;
            community.StyleProfile = request.StyleProfile?.Trim() ?? string.Empty;
            community.VkInput = request.VkInput?.Trim();

            if (!string.IsNullOrWhiteSpace(request.VkInput))
            {
                try
                {
                    var vkInfo = await _vkService.ResolveCommunityAsync(
                        request.VkInput,
                        HttpContext.RequestAborted);

                    if (vkInfo == null)
                    {
                        return BadRequest("Не удалось определить VK-сообщество по указанной ссылке или ID.");
                    }

                    bool vkCommunityChanged = community.VkGroupId != vkInfo.GroupId;

                    community.VkGroupId = vkInfo.GroupId;
                    community.VkScreenName = vkInfo.ScreenName;
                    community.VkUrl = vkInfo.Url;

                    if (vkCommunityChanged)
                    {
                        community.VkPostsSyncedAt = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Ошибка при распознавании VK-сообщества. CommunityId={CommunityId}, VkInput={VkInput}",
                        id,
                        request.VkInput);

                    return BadRequest("Ошибка при проверке VK-сообщества. Проверьте ссылку или ID сообщества.");
                }
            }
            else
            {
                community.VkGroupId = null;
                community.VkScreenName = null;
                community.VkUrl = null;
                community.VkPostsSyncedAt = null;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized("Пользователь не определен.");

            var community = await _context.Communities.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

            if (community == null)
                return NotFound("Сообщество не найдено.");

            _context.Communities.Remove(community);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static CommunityDto MapToDto(Community c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            TargetAudience = c.TargetAudience,
            StyleProfile = c.StyleProfile,
            VkInput = c.VkInput,
            VkGroupId = c.VkGroupId,
            VkScreenName = c.VkScreenName,
            VkUrl = c.VkUrl,
            VkPostsSyncedAt = c.VkPostsSyncedAt
        };
    }
}
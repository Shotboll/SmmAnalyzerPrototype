using Microsoft.AspNetCore.Mvc;
using SmmAnalyzerPrototype.Api.Services;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestSyncController : Controller
    {

        private readonly VkService _vkService;
        private readonly ILogger<TestSyncController> _logger;

        public TestSyncController(VkService vkService, ILogger<TestSyncController> logger)
        {
            _vkService = vkService;
            _logger = logger;
        }

        [HttpPost("sync-vk/{communityId:guid}")]
        public async Task<IActionResult> SyncVkPosts(
            Guid communityId,
            [FromQuery] long groupId,
            [FromQuery] int maxPages = 1)
        {
            try
            {
                _logger.LogInformation("🔄 Запуск синхронизации: Community={CommunityId}, Group={GroupId}",
                    communityId, groupId);

                var posts = await _vkService.SyncCommunityPostsAsync(
                    communityId,
                    groupId,
                    maxPages,
                    HttpContext.RequestAborted);

                _logger.LogInformation("✅ Синхронизация завершена. Загружено постов: {Count}", posts.Count);

                return Ok(new
                {
                    success = true,
                    syncedCount = posts.Count,
                    sample = posts.Take(3).Select(p => new { p.VkPostId, p.Text, p.PublishedAt })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка синхронизации ВКонтакте");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}

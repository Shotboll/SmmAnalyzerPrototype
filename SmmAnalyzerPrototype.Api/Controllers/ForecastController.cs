using Microsoft.AspNetCore.Mvc;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ForecastController : Controller
    {
        private readonly LlmService _llmService;

        public ForecastController(LlmService llmService) => _llmService = llmService;

        [HttpPost("forecast")]
        public async Task<IActionResult> GetForecast([FromBody] ForecastRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPostText))
                return BadRequest("Текст поста не может быть пустым.");

            var result = await _llmService.ForecastEngagementAsync(
                request.CommunityId, request.NewPostText, request.HistoryDays, HttpContext.RequestAborted);

            return Ok(result);
        }

        [HttpPost("recommendations")]
        public async Task<IActionResult> GetRecommendations([FromBody] ForecastRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPostText))
                return BadRequest("Текст поста не может быть пустым.");

            var result = await _llmService.GenerateRecommendationsAsync(
                request.CommunityId, request.NewPostText, request.HistoryDays, HttpContext.RequestAborted);

            return Ok(result);
        }
    }
}

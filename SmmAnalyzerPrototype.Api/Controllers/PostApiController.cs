using Microsoft.AspNetCore.Mvc;
using SmmAnalyzerPrototype.Api.Services;
using System.Text.Json;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PostApiController : Controller
    {
        private readonly LlmService _llmService;
        private readonly LanguageToolService _languageToolService;
        private readonly GrammarFalsePositiveFilterService _grammarFilterService;

        public PostApiController(LlmService llmService, LanguageToolService languageToolService, GrammarFalsePositiveFilterService grammarFilterService)
        {
            _llmService = llmService;
            _languageToolService = languageToolService;
            _grammarFilterService = grammarFilterService;
        }

        [HttpPost]
        public async Task<ActionResult<AnalyzePostResponse>> AnalyzePost([FromBody] AnalyzePostRequest request)
        {
            if (request == null || request.CommunityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new AnalyzePostResponse
                {
                    HasViolations = false,
                    Violations = new List<ViolationDto>(),
                    Comment = "Некорректные входные данные."
                });
            }

            var resultJson = await _llmService.AnalyzePostWithRagAsync(request.Text, request.CommunityId);

            try
            {
                var response = JsonSerializer.Deserialize<AnalyzePostResponse>(resultJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response == null)
                {
                    return Ok(new AnalyzePostResponse
                    {
                        HasViolations = false,
                        Violations = new List<ViolationDto>(),
                        Comment = "Не удалось обработать результат проверки."
                    });
                }

                response.Violations ??= new List<ViolationDto>();
                response.Comment ??= response.HasViolations
                    ? "Обнаружены возможные нарушения."
                    : "Нарушений не найдено.";

                foreach (var violation in response.Violations)
                {
                    violation.RuleShort ??= string.Empty;
                    violation.MatchedText ??= string.Empty;
                    violation.Explanation ??= string.Empty;
                }

                return Ok(response);
            }
            catch
            {
                return Ok(new AnalyzePostResponse
                {
                    HasViolations = false,
                    Violations = new List<ViolationDto>(),
                    Comment = "Не удалось корректно обработать ответ модели. Попробуйте повторить проверку."
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<EnhancedGrammarResponse>> CheckGrammar([FromBody] AnalyzePostRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Текст пуст.");

            var rawErrors = await _languageToolService.CheckTextAsync(request.Text);

            var filterResult = _grammarFilterService.Filter(rawErrors, request.Text);

            // Пока в основной UI показываем только уверенные ошибки
            var acceptedErrors = filterResult.AcceptedErrors;

            var explanations = await _llmService.ExplainGrammarErrorsAsync(request.Text, acceptedErrors);
            var explanationMap = explanations.ToDictionary(x => x.Index, x => x);

            var cards = acceptedErrors.Select((error, index) =>
            {
                explanationMap.TryGetValue(index, out var explanation);

                return new GrammarResultCardDto
                {
                    Original = error.Fragment,
                    Correction = error.Suggestion,
                    Type = error.Type,
                    Hint = explanation?.Hint ?? error.Message,
                    Explanation = explanation?.Explanation ?? string.Empty,
                    Offset = error.Offset,
                    Length = error.Length
                };
            }).ToList();

            return Ok(new EnhancedGrammarResponse
            {
                RawErrors = acceptedErrors,
                Cards = cards,
                SuspiciousCards = filterResult.SuspiciousErrors.Select(x => new GrammarResultCardDto
                {
                    Original = x.Fragment,
                    Correction = x.Suggestion,
                    Type = x.Type,
                    Hint = x.Message,
                    Explanation = "Сомнительное срабатывание, требует дополнительной проверки.",
                    Offset = x.Offset,
                    Length = x.Length
                }).ToList()
            });
        }

        [HttpPost]
        public async Task<ActionResult<StyleCheckResultDto>> CheckStyle([FromBody] StyleCheckRequest request)
        {
            var result = await _llmService.StyleCheck(request.Audience, request.Style, request.Text);
            return Ok(result);
        }
    }
}

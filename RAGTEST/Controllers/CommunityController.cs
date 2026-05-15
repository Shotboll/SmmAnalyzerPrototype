using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using System.Security.Claims;

namespace RAGTEST.Controllers
{
    [Authorize]
    public class CommunityController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CommunityController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var client = CreateApiClient();

            var response = await client.GetAsync("api/communityapi/getall");
            if (response.IsSuccessStatusCode)
            {
                var communities = await response.Content.ReadFromJsonAsync<List<CommunityDto>>();
                return View(communities ?? new());
            }

            return View(new List<CommunityDto>());
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCommunityRequest request)
        {
            if (!ModelState.IsValid)
                return View(request);

            var client = CreateApiClient();

            var response = await client.PostAsJsonAsync("api/communityapi/create", request);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var error = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError("", string.IsNullOrWhiteSpace(error)
                ? "Ошибка при создании сообщества"
                : error.Trim('"'));

            return View(request);
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var client = CreateApiClient();

            var response = await client.GetAsync($"api/communityapi/getbyid/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var community = await response.Content.ReadFromJsonAsync<CommunityDto>();
            if (community == null)
                return NotFound();

            var request = new UpdateCommunityRequest
            {
                Name = community.Name,
                TargetAudience = community.TargetAudience,
                StyleProfile = community.StyleProfile,
                VkInput = community.VkInput,
            };

            ViewBag.Id = id;
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateCommunityRequest request)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Id = id;
                return View(request);
            }

            var client = CreateApiClient();

            var response = await client.PutAsJsonAsync($"api/communityapi/update/{id}", request);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var error = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError("", string.IsNullOrWhiteSpace(error)
                ? "Ошибка при обновлении"
                : error.Trim('"'));

            ViewBag.Id = id;
            return View(request);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var client = CreateApiClient();

            var response = await client.GetAsync($"api/communityapi/getbyid/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var community = await response.Content.ReadFromJsonAsync<CommunityDto>();
            return View(community);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var client = CreateApiClient();

            var response = await client.DeleteAsync($"api/communityapi/delete/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            return RedirectToAction(nameof(Index));
        }

        private HttpClient CreateApiClient()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
                client.DefaultRequestHeaders.Add("X-User-Id", userId);

            return client;
        }
    }
}
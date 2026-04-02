using Microsoft.AspNetCore.Mvc;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;

namespace RAGTEST.Controllers
{
    public class CommunityController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CommunityController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: /Community
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync("api/communityapi/getall");
            if (response.IsSuccessStatusCode)
            {
                var communities = await response.Content.ReadFromJsonAsync<List<CommunityDto>>();
                return View(communities ?? new());
            }
            return View(new List<CommunityDto>());
        }

        // GET: /Community/Create
        public IActionResult Create() => View();

        // POST: /Community/Create
        [HttpPost]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCommunityRequest request)
        {
            if (!ModelState.IsValid) return View(request);

            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.PostAsJsonAsync("api/communityapi/create", request);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError("", "Ошибка при создании сообщества");
            return View(request);
        }

        // GET: /Community/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync($"api/communityapi/GetById/{id}");
            if (!response.IsSuccessStatusCode) return NotFound();

            var community = await response.Content.ReadFromJsonAsync<CommunityDto>();
            if (community == null) return NotFound();

            var request = new UpdateCommunityRequest
            {
                Name = community.Name,
                TargetAudience = community.TargetAudience,
                StyleProfile = community.StyleProfile
            };
            ViewBag.Id = id;
            return View(request);
        }

        // POST: /Community/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateCommunityRequest request)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Id = id;
                return View(request);
            }

            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.PutAsJsonAsync($"api/communityapi/Update/{id}", request);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError("", "Ошибка при обновлении");
            ViewBag.Id = id;
            return View(request);
        }

        // GET: /Community/Delete/{id}
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync($"api/communityapi/GetById/{id}");
            if (!response.IsSuccessStatusCode) return NotFound();

            var community = await response.Content.ReadFromJsonAsync<CommunityDto>();
            return View(community);
        }

        // POST: /Community/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            await client.DeleteAsync($"api/communityapi/delete/{id}");
            return RedirectToAction(nameof(Index));
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using SmmAnalyzerPrototype.Data.Models.DTO.Regualtion;

namespace RAGTEST.Controllers
{
    public class RegulationController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public RegulationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: /Regulation
        public async Task<IActionResult> Index(Guid? communityId)
        {
            var client = _httpClientFactory.CreateClient("Api");
            string url = communityId.HasValue
                ? $"api/regulationapi/GetAll?communityId={communityId}"
                : "api/regulationapi/GetAll";
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var regulations = await response.Content.ReadFromJsonAsync<List<RegulationDocumentDto>>();
                return View(regulations ?? new());
            }
            return View(new List<RegulationDocumentDto>());
        }

        // GET: /Regulation/Create
        public async Task<IActionResult> Create()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var communitiesResponse = await client.GetAsync("api/communityapi/GetAll");
            var communities = await communitiesResponse.Content.ReadFromJsonAsync<List<CommunityDto>>();
            ViewBag.Communities = communities.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View(new CreateRegulationRequest());
        }

        // POST: /Regulation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRegulationRequest request)
        {
            if (!ModelState.IsValid) return await ReloadCreateView(request);

            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.PostAsJsonAsync("api/regulationapi/Create", request);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError("", "Ошибка при создании регламента");
            return await ReloadCreateView(request);
        }

        private async Task<IActionResult> ReloadCreateView(CreateRegulationRequest request)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var communitiesResponse = await client.GetAsync("api/communityapi/GetAll");
            var communities = await communitiesResponse.Content.ReadFromJsonAsync<List<CommunityDto>>();
            ViewBag.Communities = communities.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();
            return View(request);
        }

        // GET: /Regulation/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync($"api/regulationapi/GetById/{id}");
            if (!response.IsSuccessStatusCode) return NotFound();

            var regulation = await response.Content.ReadFromJsonAsync<RegulationDocumentDto>();
            if (regulation == null) return NotFound();

            var request = new UpdateRegulationRequest
            {
                Title = regulation.Title,
                Content = regulation.Content,
                Category = regulation.Category,
                CommunityId = regulation.CommunityId
            };

            // Загружаем сообщества
            var communitiesResponse = await client.GetAsync("api/communityapi/GetAll");
            var communities = await communitiesResponse.Content.ReadFromJsonAsync<List<CommunityDto>>();
            ViewBag.Communities = communities.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = c.Id == regulation.CommunityId
            }).ToList();

            ViewBag.Id = id;
            return View(request);
        }

        // POST: /Regulation/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateRegulationRequest request)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Id = id;
                return await ReloadEditView(request, id);
            }

            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.PutAsJsonAsync($"api/regulationapi/Update/{id}", request);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError("", "Ошибка при обновлении");
            ViewBag.Id = id;
            return await ReloadEditView(request, id);
        }

        private async Task<IActionResult> ReloadEditView(UpdateRegulationRequest request, Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var communitiesResponse = await client.GetAsync("api/communityapi/GetAll");
            var communities = await communitiesResponse.Content.ReadFromJsonAsync<List<CommunityDto>>();
            ViewBag.Communities = communities.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = c.Id == request.CommunityId
            }).ToList();
            ViewBag.Id = id;
            return View(request);
        }

        // GET: /Regulation/Delete/{id}
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync($"api/regulationapi/GetById/{id}");
            if (!response.IsSuccessStatusCode) return NotFound();

            var regulation = await response.Content.ReadFromJsonAsync<RegulationDocumentDto>();
            return View(regulation);
        }

        // POST: /Regulation/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            await client.DeleteAsync($"api/regulationapi/Delete/{id}");
            return RedirectToAction(nameof(Index));
        }
    }
}

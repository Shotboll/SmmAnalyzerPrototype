using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGTEST.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Controllers
{
    [Authorize]
    public class PostController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PostController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient CreateApiClient()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
                client.DefaultRequestHeaders.Add("X-User-Id", userId);

            return client;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = CreateApiClient();

            var posts = await client.GetFromJsonAsync<List<PostListItemDto>>("api/postapi/getall") ?? new List<PostListItemDto>(); ;

            return View(posts);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var client = CreateApiClient();

            var communities = await client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new List<CommunityDto>();

            var model = new PostCreateModel
            {
                Communities = communities
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostCreateModel model)
        {
            var client = CreateApiClient();

            model.Communities = await client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new List<CommunityDto>();

            if (!ModelState.IsValid)
                return View(model);

            var request = new CreatePostRequest
            {
                Text = model.Text.Trim(),
                CommunityId = model.CommunityId
            };

            var response = await client.PostAsJsonAsync("api/postapi/create", request);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Не удалось создать пост.";
                return View(model);
            }

            var createdPost = await response.Content.ReadFromJsonAsync<PostDetailsDto>();

            if (createdPost == null)
            {
                ViewBag.Error = "Не удалось получить созданный пост.";
                return View(model);
            }

            return RedirectToAction(nameof(Details), new { id = createdPost.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{id}");

            if (post == null)
                return NotFound();

            var communities = await client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall") ?? new List<CommunityDto>();

            var model = new PostCreateModel
            {
                PostId = post.Id,
                Text = post.Text,
                CommunityId = post.CommunityId,
                Communities = communities
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PostCreateModel model)
        {
            var client = CreateApiClient();

            model.Communities = await client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall")
                               ?? new List<CommunityDto>();

            if (!ModelState.IsValid)
                return View(model);

            var request = new UpdatePostRequest
            {
                Text = model.Text.Trim(),
                CommunityId = model.CommunityId
            };

            var response = await client.PutAsJsonAsync($"api/postapi/update/{model.PostId}", request);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Не удалось обновить пост.";
                return View(model);
            }

            return RedirectToAction(nameof(Details), new { id = model.PostId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = CreateApiClient();

            var response = await client.DeleteAsync($"api/postapi/delete/{id}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Не удалось удалить пост.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Message"] = "Пост успешно удален.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var client = CreateApiClient();

            var post = await client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{id}");

            if (post == null)
                return NotFound();

            return View(post);
        }
    }
}
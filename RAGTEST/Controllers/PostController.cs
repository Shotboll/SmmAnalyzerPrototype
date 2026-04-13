using Microsoft.AspNetCore.Mvc;
using RAGTEST.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Controllers
{
    public class PostController : Controller
    {
        private readonly HttpClient _client;

        public PostController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("Api");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var posts = await _client.GetFromJsonAsync<List<PostListItemDto>>("api/postapi/getall")
                        ?? new List<PostListItemDto>();

            return View(posts);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall")
                              ?? new List<CommunityDto>();

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
            model.Communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall")
                               ?? new List<CommunityDto>();

            if (!ModelState.IsValid)
                return View(model);

            var request = new CreatePostRequest
            {
                Text = model.Text.Trim(),
                CommunityId = model.CommunityId
            };

            var response = await _client.PostAsJsonAsync("api/postapi/create", request);

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
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{id}");
            if (post == null)
                return NotFound();

            var communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall")
                              ?? new List<CommunityDto>();

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
            model.Communities = await _client.GetFromJsonAsync<List<CommunityDto>>("api/communityapi/getall")
                               ?? new List<CommunityDto>();

            if (!ModelState.IsValid)
                return View(model);

            var request = new UpdatePostRequest
            {
                Text = model.Text.Trim(),
                CommunityId = model.CommunityId
            };

            var response = await _client.PutAsJsonAsync($"api/postapi/update/{model.PostId}", request);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Не удалось обновить пост.";
                return View(model);
            }

            return RedirectToAction(nameof(Details), new { id = model.PostId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var post = await _client.GetFromJsonAsync<PostDetailsDto>($"api/postapi/getbyid/{id}");

            if (post == null)
                return NotFound();

            return View(post);
        }
    }
}
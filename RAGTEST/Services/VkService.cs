using Newtonsoft.Json.Linq;
using RAGTEST.Models.VkModels;
using System.Net.Http;
using VkNet;
using VkNet.Exception;
using VkNet.Model;

namespace RAGTEST.Services
{
    public class VkService
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;
        private readonly long _groupId;
        private readonly ILogger<VkService> _logger;

        public VkService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<VkService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _accessToken = config["Vk:AccessToken"];
            _groupId = long.Parse(config["Vk:GroupId"]);
            _logger = logger;
        }

        private string BuildWallGetUrl(int count = 30)
        {
            return $"https://api.vk.com/method/wall.get?owner_id=-{_groupId}&count={count}&access_token={_accessToken}&v=5.199";
        }

        public async Task<List<VkPostDto>> GetLatestPostsAsync(int count = 30)
        {
            try
            {
                string url = BuildWallGetUrl(count);
                _logger.LogInformation("Запрос к VK API: {Url}", url.Replace(_accessToken, "HIDDEN"));

                string responseJson = await _httpClient.GetStringAsync(url);
                _logger.LogDebug("Ответ VK: {Response}", responseJson);

                var json = JObject.Parse(responseJson);

                if (json["error"] != null)
                {
                    int errorCode = json["error"]["error_code"].Value<int>();
                    string errorMsg = json["error"]["error_msg"].Value<string>();
                    throw new Exception($"VK API error {errorCode}: {errorMsg}");
                }

                var items = json["response"]["items"] as JArray;
                if (items == null)
                    return new List<VkPostDto>();

                var posts = new List<VkPostDto>();
                foreach (var item in items)
                {
                    posts.Add(new VkPostDto
                    {
                        Id = item["id"].Value<long>(),
                        Text = item["text"]?.Value<string>() ?? "",
                        Date = DateTimeOffset.FromUnixTimeSeconds(item["date"].Value<long>()).DateTime,
                        Likes = item["likes"]?["count"]?.Value<int>() ?? 0,
                        Comments = item["comments"]?["count"]?.Value<int>() ?? 0,
                        Reposts = item["reposts"]?["count"]?.Value<int>() ?? 0,
                        Views = item["views"]?["count"]?.Value<int>() ?? 0,
                        IsPinned = item["is_pinned"]?.Value<int>() == 1,
                        MarkedAsAds = item["marked_as_ads"]?.Value<int>() == 1
                    });
                }

                _logger.LogInformation("Успешно получено {Count} постов", posts.Count);
                return posts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении постов VK");
                throw;
            }
        }

    }
}

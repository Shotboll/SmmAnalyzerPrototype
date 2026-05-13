using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SmmAnalyzerPrototype.Api.Models.VkModels;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using System.Net.Http;
using VkNet;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Utils;

namespace SmmAnalyzerPrototype.Api.Services
{
    public class VkService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VkService> _logger;
        private readonly string _accessToken;

        public VkService(AppDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<VkService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _accessToken = _configuration["Vk:AccessToken"]
                ?? throw new InvalidOperationException("Ключ 'Vk:AccessToken' не найден в конфигурации.");
        }

        /// <summary>
        /// Синхронизация постов сообщества с ВКонтакте.
        /// </summary>
        public async Task<List<CommunityPost>> SyncCommunityPostsAsync(
            Guid communityId,
            long groupId,
            int maxPages = 3,
            CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(2);

            var result = new List<CommunityPost>();
            int offset = 0;
            const int countPerPage = 100;

            for (int page = 0; page < maxPages && !ct.IsCancellationRequested; page++)
            {
                var url = $"https://api.vk.com/method/wall.get?owner_id=-{groupId}&count={countPerPage}&offset={offset}&extended=1&v=5.199&access_token={_accessToken}";

                try
                {
                    _logger.LogDebug("Запрос к VK API: страница {Page}", page + 1);
                    var responseJson = await client.GetStringAsync(url, ct);
                    var json = JObject.Parse(responseJson);

                    // Проверка на ошибки VK API
                    if (json["error"] != null)
                    {
                        int errorCode = json["error"]["error_code"].Value<int>();
                        string errorMsg = json["error"]["error_msg"].Value<string>();
                        throw new Exception($"VK API error {errorCode}: {errorMsg}");
                    }

                    var items = json["response"]?["items"] as JArray;
                    if (items == null || items.Count == 0) break;

                    foreach (var item in items)
                    {
                        long postId = item["id"].Value<long>();

                        // Читаем текст
                        var postText = item["text"]?.Value<string>() ?? string.Empty;

                        // пропускаем посты без текста
                        if (string.IsNullOrWhiteSpace(postText))
                            continue;

                        // Дедупликация по связке CommunityId + VkPostId
                        var exists = await _dbContext.CommunityPosts
                            .AnyAsync(cp => cp.CommunityId == communityId && cp.VkPostId == postId, ct);
                        if (exists) continue;

                        // Пропускаем рекламу и закреплённые
                        if (item["marked_as_ads"]?.Value<int>() == 1 || item["is_pinned"]?.Value<int>() == 1)
                            continue;

                        result.Add(new CommunityPost
                        {
                            Id = Guid.NewGuid(),
                            CommunityId = communityId,
                            Source = "vk",
                            VkPostId = postId,
                            Text = item["text"]?.Value<string>() ?? string.Empty,
                            PublishedAt = DateTimeOffset.FromUnixTimeSeconds(item["date"].Value<long>()).UtcDateTime,
                            Likes = item["likes"]?["count"]?.Value<int>() ?? 0,
                            Comments = item["comments"]?["count"]?.Value<int>() ?? 0,
                            Reposts = item["reposts"]?["count"]?.Value<int>() ?? 0,
                            Views = item["views"]?["count"]?.Value<int>() ?? 0
                        });
                    }

                    // Если вернулось меньше записей → посты закончились
                    if (items.Count < countPerPage) break;

                    offset += countPerPage;
                    await Task.Delay(350, ct); // ~3 запроса/сек
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при запросе к VK API (страница {Page})", page + 1);
                    break;
                }
            }

            if (result.Any())
            {
                await _dbContext.CommunityPosts.AddRangeAsync(result, ct);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Сохранено {Count} новых постов для сообщества {CommunityId}",
                    result.Count, communityId);
            }

            return result;
        }
    }
}

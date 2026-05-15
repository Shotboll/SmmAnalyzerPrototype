using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SmmAnalyzerPrototype.Api.Models.VkModels;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using System.Linq;

namespace SmmAnalyzerPrototype.Api.Services
{
    public class VkService
    {
        private const string VkApiVersion = "5.199";
        private const int CountPerPage = 100;

        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VkService> _logger;
        private readonly string _accessToken;

        public VkService( AppDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<VkService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _accessToken = _configuration["Vk:AccessToken"]
                ?? throw new InvalidOperationException("Ключ 'Vk:AccessToken' не найден в конфигурации.");
        }

        public async Task<VkCommunityResolveResult?> ResolveCommunityAsync(string? input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            string normalized = NormalizeVkInput(input);

            if (TryExtractNumericGroupId(normalized, out long directGroupId))
            {
                return await GetGroupInfoByIdAsync(directGroupId.ToString(), ct);
            }

            string screenName = ExtractScreenName(normalized);

            if (string.IsNullOrWhiteSpace(screenName))
                return null;

            var resolved = await ResolveByScreenNameAsync(screenName, ct);

            if (resolved != null)
                return resolved;

            return await GetGroupInfoByIdAsync(screenName, ct);
        }

        public async Task<int> EnsureCommunityPostsSyncedAsync( Guid communityId, int maxPages = 3, int minExistingPosts = 30, TimeSpan? refreshInterval = null, CancellationToken ct = default)
        {
            refreshInterval ??= TimeSpan.FromHours(12);

            var community = await _dbContext.Communities
                .FirstOrDefaultAsync(c => c.Id == communityId, ct);

            if (community == null)
                throw new InvalidOperationException("Сообщество не найдено.");

            if (!community.VkGroupId.HasValue)
                throw new InvalidOperationException("Для сообщества не указан VK ID или ссылка на сообщество.");

            int existingPostsCount = await _dbContext.CommunityPosts
                .CountAsync(cp => cp.CommunityId == communityId && cp.Source == "vk", ct);

            bool hasEnoughPosts = existingPostsCount >= minExistingPosts;
            bool recentlySynced = community.VkPostsSyncedAt.HasValue
                && DateTime.UtcNow - community.VkPostsSyncedAt.Value < refreshInterval.Value;

            if (hasEnoughPosts && recentlySynced)
            {
                _logger.LogInformation(
                    "Синхронизация VK пропущена. CommunityId={CommunityId}, ExistingPosts={ExistingPostsCount}, LastSync={LastSync}",
                    communityId,
                    existingPostsCount,
                    community.VkPostsSyncedAt);

                return 0;
            }

            var posts = await SyncCommunityPostsAsync(
                communityId,
                community.VkGroupId.Value,
                maxPages,
                ct);

            community.VkPostsSyncedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return posts.Count;
        }

        public async Task<List<CommunityPost>> SyncCommunityPostsAsync(Guid communityId, long groupId, int maxPages = 3, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(2);

            var result = new List<CommunityPost>();
            int offset = 0;

            for (int page = 0; page < maxPages && !ct.IsCancellationRequested; page++)
            {
                string url = BuildVkMethodUrl("wall.get", new Dictionary<string, string>
                {
                    ["owner_id"] = (-groupId).ToString(),
                    ["count"] = CountPerPage.ToString(),
                    ["offset"] = offset.ToString(),
                    ["extended"] = "1",
                    ["filter"] = "owner"
                });

                try
                {
                    _logger.LogDebug(
                        "Запрос wall.get к VK API. CommunityId={CommunityId}, GroupId={GroupId}, Page={Page}, Offset={Offset}",
                        communityId,
                        groupId,
                        page + 1,
                        offset);

                    string responseJson = await client.GetStringAsync(url, ct);
                    JObject json = JObject.Parse(responseJson);

                    ThrowIfVkError(json);

                    var items = json["response"]?["items"] as JArray;

                    if (items == null || items.Count == 0)
                        break;

                    var postIdsFromPage = items
                        .Select(item => item["id"]?.Value<long>() ?? 0)
                        .Where(id => id > 0)
                        .ToList();

                    var existingVkPostIds = await _dbContext.CommunityPosts
                        .Where(cp => cp.CommunityId == communityId && cp.Source == "vk" && postIdsFromPage.Contains((long)cp.VkPostId!))
                        .Select(cp => cp.VkPostId)
                        .ToListAsync(ct);

                    var existingSet = existingVkPostIds.ToHashSet();

                    foreach (var item in items)
                    {
                        long postId = item["id"]?.Value<long>() ?? 0;

                        if (postId <= 0)
                            continue;

                        if (existingSet.Contains(postId))
                            continue;

                        string postText = item["text"]?.Value<string>() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(postText))
                            continue;

                        if (item["marked_as_ads"]?.Value<int>() == 1)
                            continue;

                        if (item["is_pinned"]?.Value<int>() == 1)
                            continue;

                        long unixDate = item["date"]?.Value<long>() ?? 0;

                        if (unixDate <= 0)
                            continue;

                        var communityPost = new CommunityPost
                        {
                            Id = Guid.NewGuid(),
                            CommunityId = communityId,
                            Source = "vk",
                            VkPostId = postId,
                            Text = postText.Trim(),
                            PublishedAt = DateTimeOffset.FromUnixTimeSeconds(unixDate).UtcDateTime,
                            Likes = item["likes"]?["count"]?.Value<int>() ?? 0,
                            Comments = item["comments"]?["count"]?.Value<int>() ?? 0,
                            Reposts = item["reposts"]?["count"]?.Value<int>() ?? 0,
                            Views = item["views"]?["count"]?.Value<int>() ?? 0
                        };

                        result.Add(communityPost);
                        existingSet.Add(postId);
                    }

                    if (items.Count < CountPerPage)
                        break;

                    offset += CountPerPage;

                    await Task.Delay(350, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Ошибка при запросе VK API. CommunityId={CommunityId}, GroupId={GroupId}, Page={Page}",
                        communityId,
                        groupId,
                        page + 1);

                    break;
                }
            }

            if (result.Count > 0)
            {
                await _dbContext.CommunityPosts.AddRangeAsync(result, ct);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Сохранено {Count} новых VK-постов для сообщества {CommunityId}",
                    result.Count,
                    communityId);
            }

            return result;
        }

        private async Task<VkCommunityResolveResult?> ResolveByScreenNameAsync(string screenName, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();

            string url = BuildVkMethodUrl("utils.resolveScreenName", new Dictionary<string, string>
            {
                ["screen_name"] = screenName
            });

            string responseJson = await client.GetStringAsync(url, ct);
            JObject json = JObject.Parse(responseJson);

            ThrowIfVkError(json);

            var response = json["response"];

            if (response == null || response.Type == JTokenType.Array)
                return null;

            string type = response["type"]?.Value<string>() ?? string.Empty;
            long objectId = response["object_id"]?.Value<long>() ?? 0;

            if (objectId <= 0)
                return null;

            if (type != "group" && type != "page")
                return null;

            var info = await GetGroupInfoByIdAsync(objectId.ToString(), ct);

            if (info != null)
                return info;

            return new VkCommunityResolveResult
            {
                GroupId = objectId,
                ScreenName = screenName,
                Url = $"https://vk.com/{screenName}",
                Name = screenName
            };
        }

        private async Task<VkCommunityResolveResult?> GetGroupInfoByIdAsync( string groupIdOrScreenName, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();

            string url = BuildVkMethodUrl("groups.getById", new Dictionary<string, string>
            {
                ["group_id"] = groupIdOrScreenName,
                ["fields"] = "screen_name"
            });

            string responseJson = await client.GetStringAsync(url, ct);
            JObject json = JObject.Parse(responseJson);

            ThrowIfVkError(json);

            JToken? groupToken = null;

            if (json["response"] is JArray responseArray)
            {
                groupToken = responseArray.FirstOrDefault();
            }
            else if (json["response"]?["groups"] is JArray groupsArray)
            {
                groupToken = groupsArray.FirstOrDefault();
            }
            else if (json["response"] is JObject responseObject)
            {
                groupToken = responseObject;
            }

            if (groupToken == null)
                return null;

            long groupId = groupToken["id"]?.Value<long>() ?? 0;

            if (groupId <= 0)
                return null;

            string screenName = groupToken["screen_name"]?.Value<string>() ?? groupId.ToString();
            string name = groupToken["name"]?.Value<string>() ?? screenName;

            return new VkCommunityResolveResult
            {
                GroupId = groupId,
                ScreenName = screenName,
                Url = $"https://vk.com/{screenName}",
                Name = name
            };
        }

        private string BuildVkMethodUrl(string method, Dictionary<string, string> parameters)
        {
            var query = new List<string>();

            foreach (var pair in parameters)
            {
                query.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
            }

            query.Add($"access_token={Uri.EscapeDataString(_accessToken)}");
            query.Add($"v={Uri.EscapeDataString(VkApiVersion)}");

            return $"https://api.vk.com/method/{method}?{string.Join("&", query)}";
        }

        private static void ThrowIfVkError(JObject json)
        {
            if (json["error"] == null)
                return;

            int errorCode = json["error"]?["error_code"]?.Value<int>() ?? 0;
            string errorMessage = json["error"]?["error_msg"]?.Value<string>() ?? "Неизвестная ошибка VK API";

            throw new InvalidOperationException($"VK API error {errorCode}: {errorMessage}");
        }

        private static string NormalizeVkInput(string input)
        {
            string value = input.Trim();

            value = value.Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("m.vk.com/", "vk.com/", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("www.vk.com/", "vk.com/", StringComparison.OrdinalIgnoreCase);

            if (value.StartsWith("@"))
                value = value[1..];

            return value.Trim().Trim('/');
        }

        private static string ExtractScreenName(string normalizedInput)
        {
            string value = normalizedInput.Trim();

            if (value.StartsWith("vk.com/", StringComparison.OrdinalIgnoreCase))
                value = value["vk.com/".Length..];

            int queryIndex = value.IndexOf('?');

            if (queryIndex >= 0)
                value = value[..queryIndex];

            int slashIndex = value.IndexOf('/');

            if (slashIndex >= 0)
                value = value[..slashIndex];

            return value.Trim();
        }

        private static bool TryExtractNumericGroupId(string normalizedInput, out long groupId)
        {
            groupId = 0;

            string value = ExtractScreenName(normalizedInput);

            if (long.TryParse(value, out long rawNumber) && rawNumber > 0)
            {
                groupId = rawNumber;
                return true;
            }

            if (value.StartsWith("club", StringComparison.OrdinalIgnoreCase))
            {
                string numberPart = value[4..];

                if (long.TryParse(numberPart, out long clubId) && clubId > 0)
                {
                    groupId = clubId;
                    return true;
                }
            }

            if (value.StartsWith("public", StringComparison.OrdinalIgnoreCase))
            {
                string numberPart = value[6..];

                if (long.TryParse(numberPart, out long publicId) && publicId > 0)
                {
                    groupId = publicId;
                    return true;
                }
            }

            return false;
        }
    }
}
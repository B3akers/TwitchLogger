using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace TwitchLogger.SimpleGraphQL
{
    public static class TwitchGraphQL
    {
        private readonly static HttpClient _httpClient = new HttpClient();

        private readonly static string UseLiveSHA256 = "639d5f11bfb8bf3053b424d9ef650d04c4ebb7d94711d644afb08fe9a0fad5d9";
        private readonly static string ViewerFeedback_CreatorSHA256 = "0927ff9e12f5730f3deb9d9fbe1f7bcbbb65101fa2c3a0a4543cd00b83a3b553";
        private readonly static string ChatList_BadgesSHA256 = "86f43113c04606e6476e39dcd432dee47c994d77a83e54b732e11d4935f0cd08";
        private readonly static string BrowsePage_PopularBadgesSHA256 = "267d2d2a64e0a0d6206c039ea9948d14a9b300a927d52b2efc52d2486ff0ec65";

        private const int MAX_OPERATIONS_IN_REQUEST = 35;

        static TwitchGraphQL()
        {
            _httpClient.DefaultRequestHeaders.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");
        }

        public static void AddDefaultRequestHeader(string name, string value)
        {
            _httpClient.DefaultRequestHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        }

        public static async Task<string> GetUserID(string login)
        {
            JArray operations = new JArray
            {
                GetUseLiveObject(login)
            };

            var result = await MakeGraphQLRequest(operations);
            var user = result[0]["data"]["user"];
            if (user.Type == JTokenType.Null)
                return string.Empty;

            return user["id"].ToString();
        }

        public static async Task<List<string>> GetUsersIds(IEnumerable<string> logins)
        {
            var useLiveObjects = logins.Select(x => GetUseLiveObject(x)).ToList();
            List<string> result = new List<string>();

            for (var i = 0; i < useLiveObjects.Count; i += MAX_OPERATIONS_IN_REQUEST)
            {
                JArray operations = new JArray
                {
                    useLiveObjects.Skip(i).Take(MAX_OPERATIONS_IN_REQUEST)
                };

                var resultRequest = await MakeGraphQLRequest(operations);
                result.AddRange(resultRequest.Select(x =>
                {
                    var user = x["data"]["user"];
                    if (user.Type == JTokenType.Null)
                        return string.Empty;

                    return user["id"].ToString();
                }));
            }

            return result;
        }

        public static async Task<string> GetClientIntegrity()
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = new Uri("https://gql.twitch.tv/integrity");
            requestMessage.Method = HttpMethod.Post;

            var result = await _httpClient.SendAsync(requestMessage);

            result.EnsureSuccessStatusCode();

            var stringData = await result.Content.ReadAsStringAsync();
            var jsonData = JObject.Parse(stringData);

            return jsonData["token"].ToString();
        }

        public static async Task<List<TwitchLiveChannel>> GetLiveChannels(string cursor)
        {
            JArray operations = new JArray
            {
                GetBrowsePage_PopularObject(cursor)
            };

            var listResult = new List<TwitchLiveChannel>();

            var result = await MakeGraphQLRequest(operations);
            Console.WriteLine(result.ToString());
            var edgesList = result[0]["data"]["streams"]["edges"] as JArray;
            foreach (var edge in edgesList)
            {
                listResult.Add(new TwitchLiveChannel()
                {
                    Id = (string)edge["node"]["broadcaster"]["id"],
                    DisplayName = (string)edge["node"]["broadcaster"]["displayName"],
                    Login = (string)edge["node"]["broadcaster"]["login"],
                    Cursor = (string)edge["cursor"]
                });
            }

            return listResult;
        }

        public static async Task<List<TwitchBadge>> GetChannelBadgesInfo(string channelLogin)
        {
            JArray operations = new JArray
            {
                GetChatList_BadgesObject(channelLogin)
            };

            var result = await MakeGraphQLRequest(operations);
            var data = result[0]["data"];
            var globalBadges = data["badges"].ToObject<List<TwitchBadge>>();
            var user = data["user"];
            if (user != null && user.Type != JTokenType.Null)
            {
                var broadcastBadges = data["user"]["broadcastBadges"].ToObject<List<TwitchBadge>>();
                globalBadges.AddRange(broadcastBadges);
            }
            return globalBadges;
        }

        public static async Task<TwitchUser> GetUserInfoById(string userId)
        {
            JArray operations = new JArray
            {
                GetViewerFeedback_CreatorObject(userId)
            };

            var result = await MakeGraphQLRequest(operations);
            return result[0]["data"]["creator"].ToObject<TwitchUser>();
        }

        public static async Task<List<TwitchUser>> GetUsersInfoById(IEnumerable<string> userIds)
        {
            var getViewerFeedback_CreatorObjects = userIds.Select(x => GetViewerFeedback_CreatorObject(x)).ToList();
            List<TwitchUser> result = new List<TwitchUser>();

            for (var i = 0; i < getViewerFeedback_CreatorObjects.Count; i += MAX_OPERATIONS_IN_REQUEST)
            {
                JArray operations = new JArray
                {
                    getViewerFeedback_CreatorObjects.Skip(i).Take(MAX_OPERATIONS_IN_REQUEST)
                };

                var resultRequest = await MakeGraphQLRequest(operations);
                result.AddRange(resultRequest.Select(x =>
                {
                    var user = x["data"]["creator"];
                    if (user.Type == JTokenType.Null)
                        return new TwitchUser();

                    return user.ToObject<TwitchUser>();
                }));
            }

            return result;
        }

        private static async Task<JArray> MakeGraphQLRequest(JArray operations)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = new Uri("https://gql.twitch.tv/gql");
            requestMessage.Method = HttpMethod.Post;
            requestMessage.Content = new StringContent(operations.ToString(), Encoding.UTF8, "application/json");

            var result = await _httpClient.SendAsync(requestMessage);

            result.EnsureSuccessStatusCode();

            var dataString = await result.Content.ReadAsStringAsync();

            return JArray.Parse(dataString);
        }

        private static JObject GetBrowsePage_PopularObject(string cursor)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();
            JObject options = new JObject();
            JObject recommendationsContext = new JObject();

            if (!string.IsNullOrEmpty(cursor))
                variables["cursor"] = cursor;

            variables["limit"] = 30;
            variables["platformType"] = "all";
            variables["options"] = options;

            options["recommendationsContext"] = recommendationsContext;
            recommendationsContext["platform"] = "web";
            options["sort"] = "VIEWER_COUNT";
            options["tags"] = new JArray();

            variables["sortTypeIsRecency"] = false;
            variables["freeformTagsEnabled"] = false;

            obj["operationName"] = "BrowsePage_Popular";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(BrowsePage_PopularBadgesSHA256);

            return obj;
        }

        private static JObject GetUseLiveObject(string login)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelLogin"] = login;

            obj["operationName"] = "UseLive";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(UseLiveSHA256);

            return obj;
        }

        private static JObject GetChatList_BadgesObject(string login)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelLogin"] = login;

            obj["operationName"] = "ChatList_Badges";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(ChatList_BadgesSHA256);

            return obj;
        }

        private static JObject GetViewerFeedback_CreatorObject(string userId)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelID"] = userId;

            obj["operationName"] = "ViewerFeedback_Creator";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(ViewerFeedback_CreatorSHA256);

            return obj;
        }

        private static JObject GetExtensionsForObject(string sha256)
        {
            JObject extensions = new JObject();
            JObject persistedQuery = new JObject();
            persistedQuery["version"] = 1;
            persistedQuery["sha256Hash"] = sha256;
            extensions["persistedQuery"] = persistedQuery;

            return extensions;
        }
    }
}
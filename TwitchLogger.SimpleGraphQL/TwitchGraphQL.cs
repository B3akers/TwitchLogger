using Newtonsoft.Json.Linq;
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

        private const int MAX_OPERATIONS_IN_REQUEST = 35;

        static TwitchGraphQL()
        {
            _httpClient.DefaultRequestHeaders.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");
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

        private static JObject GetUseLiveObject(string login)
        {
            JObject useLiveObject = new JObject();
            JObject variables = new JObject();

            variables["channelLogin"] = login;

            useLiveObject["operationName"] = "UseLive";
            useLiveObject["variables"] = variables;
            useLiveObject["extensions"] = GetExtensionsForObject(UseLiveSHA256);

            return useLiveObject;
        }

        private static JObject GetViewerFeedback_CreatorObject(string userId)
        {
            JObject useLiveObject = new JObject();
            JObject variables = new JObject();

            variables["channelID"] = userId;

            useLiveObject["operationName"] = "ViewerFeedback_Creator";
            useLiveObject["variables"] = variables;
            useLiveObject["extensions"] = GetExtensionsForObject(ViewerFeedback_CreatorSHA256);

            return useLiveObject;
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
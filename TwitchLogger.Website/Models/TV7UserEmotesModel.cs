using System.Text.Json.Serialization;

namespace TwitchLogger.Website.Models
{
    public class TV7UserEmotesConnection
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("linked_at")]
        public long LinkedAt { get; set; }

        [JsonPropertyName("emote_capacity")]
        public int EmoteCapacity { get; set; }

        [JsonPropertyName("emote_set_id")]
        public object EmoteSetId { get; set; }

        [JsonPropertyName("emote_set")]
        public TV7UserEmotesEmoteSet EmoteSet { get; set; }
    }

    public class TV7UserEmotesData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("flags")]
        public int Flags { get; set; }

        [JsonPropertyName("lifecycle")]
        public int Lifecycle { get; set; }

        [JsonPropertyName("state")]
        public List<string> State { get; set; }

        [JsonPropertyName("listed")]
        public bool Listed { get; set; }

        [JsonPropertyName("animated")]
        public bool Animated { get; set; }

        [JsonPropertyName("owner")]
        public TV7UserEmotesOwner Owner { get; set; }

        [JsonPropertyName("host")]
        public TV7UserEmotesHost Host { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
    }

    public class Editor
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("permissions")]
        public int Permissions { get; set; }

        [JsonPropertyName("visible")]
        public bool Visible { get; set; }

        [JsonPropertyName("added_at")]
        public object AddedAt { get; set; }
    }

    public class Emote
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("flags")]
        public int Flags { get; set; }

        [JsonPropertyName("timestamp")]
        public object Timestamp { get; set; }

        [JsonPropertyName("actor_id")]
        public object ActorId { get; set; }

        [JsonPropertyName("data")]
        public TV7UserEmotesData Data { get; set; }
    }

    public class TV7UserEmotesEmoteSet
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("flags")]
        public int Flags { get; set; }

        [JsonPropertyName("tags")]
        public List<object> Tags { get; set; }

        [JsonPropertyName("immutable")]
        public bool Immutable { get; set; }

        [JsonPropertyName("privileged")]
        public bool Privileged { get; set; }

        [JsonPropertyName("emotes")]
        public List<Emote> Emotes { get; set; }

        [JsonPropertyName("emote_count")]
        public int EmoteCount { get; set; }

        [JsonPropertyName("capacity")]
        public int Capacity { get; set; }

        [JsonPropertyName("owner")]
        public TV7UserEmotesOwner Owner { get; set; }
    }

    public class TV7UserEmotesFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("static_name")]
        public string StaticName { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("frame_count")]
        public int FrameCount { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; }
    }

    public class TV7UserEmotesHost
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("files")]
        public List<TV7UserEmotesFile> Files { get; set; }
    }

    public class TV7UserEmotesOwner
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("style")]
        public TV7UserEmotesStyle Style { get; set; }

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; }
    }

    public class TV7UserEmotes
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("linked_at")]
        public long LinkedAt { get; set; }

        [JsonPropertyName("emote_capacity")]
        public int EmoteCapacity { get; set; }

        [JsonPropertyName("emote_set_id")]
        public object EmoteSetId { get; set; }

        [JsonPropertyName("emote_set")]
        public TV7UserEmotesEmoteSet EmoteSet { get; set; }

        [JsonPropertyName("user")]
        public TV7UserEmotesUser User { get; set; }
    }

    public class TV7UserEmotesStyle
    {
        [JsonPropertyName("color")]
        public int? Color { get; set; }
    }

    public class TV7UserEmotesUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("biography")]
        public string Biography { get; set; }

        [JsonPropertyName("style")]
        public TV7UserEmotesStyle Style { get; set; }

        [JsonPropertyName("editors")]
        public List<Editor> Editors { get; set; }

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; }

        [JsonPropertyName("connections")]
        public List<TV7UserEmotesConnection> Connections { get; set; }
    }
}

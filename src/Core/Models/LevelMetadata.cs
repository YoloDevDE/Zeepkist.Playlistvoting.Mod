using Lombok.NET;
using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

[NoArgsConstructor]
[AllArgsConstructor]
public partial class LevelMetadata
{
    [JsonProperty("uid")] public string Uid { get; set; } = string.Empty;

    [JsonProperty("levelUid")]
    private string LevelUid
    {
        set => Uid = value;
    }

    [JsonProperty("UID")]
    private string UID
    {
        set => Uid = value;
    }

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("levelName")]
    private string LevelName
    {
        set => Name = value;
    }

    [JsonProperty("Name")]
    private string NamePascal
    {
        set => Name = value;
    }

    [JsonProperty("author")] public string Author { get; set; } = string.Empty;

    [JsonProperty("levelAuthor")]
    private string LevelAuthor
    {
        set => Author = value;
    }

    [JsonProperty("Author")]
    private string AuthorPascal
    {
        set => Author = value;
    }

    [JsonProperty("workshopID")] public ulong? WorkshopId { get; set; }

    [JsonProperty("workshopId")]
    private ulong? WorkshopIdLower
    {
        set => WorkshopId = value;
    }

    [JsonProperty("WorkshopID")]
    private ulong? WorkshopIDPascal
    {
        set => WorkshopId = value;
    }
}
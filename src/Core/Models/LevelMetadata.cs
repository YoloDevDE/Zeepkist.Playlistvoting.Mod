using Lombok.NET;
using Newtonsoft.Json;

namespace PlaylistVoting.Core.Models;

[NoArgsConstructor]
[AllArgsConstructor]
public partial class LevelMetadata
{
    [JsonProperty("uid")] public string Uid { get; set; } = string.Empty;

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("author")] public string Author { get; set; } = string.Empty;

    [JsonProperty("workshopID")] public ulong? WorkshopId { get; set; }
}
using System.Collections.Generic;
using System.Linq;
using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class PlaylistSyncService
{
    public PlaylistComparisonResult Compare(List<LevelMetadata> local, List<LevelMetadata> online)
    {
        HashSet<string> localUids = local.Select(l => l.Uid).ToHashSet();
        HashSet<string> onlineUids = online.Select(l => l.Uid).ToHashSet();

        bool areEqual = localUids.SetEquals(onlineUids);
        return new PlaylistComparisonResult
        {
            AreEqual = areEqual,

            LocalOnly = local.Where(l => !onlineUids.Contains(l.Uid)).ToList(),
            OnlineOnly = online.Where(l => !localUids.Contains(l.Uid)).ToList()
        };
    }

    public List<LevelMetadata> Merge(List<LevelMetadata> local, List<LevelMetadata> online)
    {
        // Use the playlist with more levels as the base.
        List<LevelMetadata> baseList = local.Count >= online.Count ? local : online;
        List<LevelMetadata> otherList = baseList == local ? online : local;

        List<LevelMetadata> result = baseList.ToList();
        HashSet<string> existingUids = baseList.Select(l => l.Uid).ToHashSet();

        foreach (LevelMetadata level in otherList)
        {
            if (!existingUids.Contains(level.Uid))
            {
                result.Add(level);
                existingUids.Add(level.Uid);
            }
        }

        return result;
    }

    public List<LevelMetadata> Deduplicate(List<LevelMetadata> playlist)
    {
        List<LevelMetadata> result = new List<LevelMetadata>();
        HashSet<string> seenUids = new HashSet<string>();

        foreach (LevelMetadata level in playlist)
        {
            if (seenUids.Add(level.Uid))
            {
                result.Add(level);
            }
        }

        return result;
    }
}

public class PlaylistComparisonResult
{
    public bool AreEqual { get; set; }
    public List<LevelMetadata> LocalOnly { get; set; }
    public List<LevelMetadata> OnlineOnly { get; set; }
}
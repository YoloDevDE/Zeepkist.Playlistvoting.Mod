using PlaylistVoting.Data.Models;
using ZeepkistClient;

namespace PlaylistVoting.Networking.Zeepkist;

public static class ZeepkistMetadataProvider
{
    public static LevelMetadata GetCurrentLevelMetadata()
    {
        LevelMetadata level = new LevelMetadata();

        if (PlayerManager.Instance?.currentMaster?.GlobalLevel != null)
        {
            level.Name = PlayerManager.Instance.currentMaster.GlobalLevel.Name;
            level.Author = PlayerManager.Instance.currentMaster.GlobalLevel.Author;
        }

        if (ZeepkistNetwork.CurrentLobby != null)
        {
            level.Uid = ZeepkistNetwork.CurrentLobby.LevelUID;
            level.WorkshopId = ZeepkistNetwork.CurrentLobby.WorkshopID;
        }

        return level;
    }

    public static string GetLobbyTimeLeft() => ZeepkistNetwork.CurrentLobby?.timeLeftString ?? "--:--";
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using PlaylistVoting.Core.Models;
using ZeepkistNetworking;
using ZeepSDK.Playlist;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class PlaylistFileService
{
    private readonly ManualLogSource _logger;
    private readonly string _playlistsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zeepkist", "Playlists");

    public PlaylistFileService(ManualLogSource logger)
    {
        _logger = logger;
    }

    public void SavePlaylist(string name, List<LevelMetadata> levels, int roundLength = 360, bool shuffle = false)
    {
        PlaylistSaveJSON playlistSaveJson = PlaylistApi.CreatePlaylist(name);
        IPlaylistEditor playlistEditor = playlistSaveJson.CreateEditor();

        playlistEditor.Shuffle = shuffle;
        playlistEditor.RoundLength = roundLength;

        foreach (LevelMetadata level in levels)
        {
            OnlineZeeplevel onlineLevel = new OnlineZeeplevel
            {
                UID = level.Uid,
                Name = level.Name,
                Author = level.Author,
                WorkshopID = level.WorkshopId ?? 0
            };
            playlistEditor.AddLevel(onlineLevel);
        }

        playlistEditor.Save();
    }

    public List<LevelMetadata> LoadLocalPlaylist(string name)
    {
        if (!PlaylistApi.Exists(name))
        {
            return new List<LevelMetadata>();
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(name);
        return playlist.levels.Select(l => new LevelMetadata
        {
            Uid = l.UID,
            Name = l.Name,
            Author = l.Author,
            WorkshopId = l.WorkshopID
        }).ToList();
    }

    public List<LevelMetadata> GetCurrentZeepkistPlaylist() =>
        // This is tricky because there's no direct API to get the *currently loaded* playlist in Zeepkist easily via ZeepSDK yet?
        // Actually, we might need to check how Zeepkist stores it.
        // For now, let's return an empty list or try to find it.
        new List<LevelMetadata>();
}
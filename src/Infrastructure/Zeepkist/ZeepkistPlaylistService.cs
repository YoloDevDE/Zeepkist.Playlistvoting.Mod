using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Newtonsoft.Json;
using PlaylistVoting.Core.Models;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Playlist;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class ZeepkistPlaylistService
{
    private readonly ManualLogSource _logger;
    private readonly string _playlistsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zeepkist", "Playlists");

    public ZeepkistPlaylistService(ManualLogSource logger)
    {
        _logger = logger;
    }

    public void SavePlaylist(string name, List<OnlineZeeplevelDto> levels, int roundLength = 360, bool shuffle = false)
    {
        PlaylistSaveJSON playlistSaveJson = PlaylistApi.CreatePlaylist(name);
        IPlaylistEditor playlistEditor = playlistSaveJson.CreateEditor();

        playlistEditor.Shuffle = shuffle;
        playlistEditor.RoundLength = roundLength;

        foreach (OnlineZeeplevelDto level in levels)
        {
            OnlineZeeplevel onlineLevel = new OnlineZeeplevel
            {
                UID = level.UID,
                Name = level.Name,
                Author = level.Author,
                WorkshopID = level.WorkshopID,
                Collaborators = level.Collaborators,
                OverrideAuthorName = level.OverrideAuthorName,
                played = level.played
            };
            playlistEditor.AddLevel(onlineLevel);
        }

        playlistEditor.Save();
    }

    public void SavePlaylist(string name, List<LevelMetadata> levels, int roundLength = 360, bool shuffle = false)
    {
        SavePlaylist(name, levels.Select(l => new OnlineZeeplevelDto
        {
            UID = l.Uid,
            Name = l.Name,
            Author = l.Author,
            WorkshopID = l.WorkshopId ?? 0
        }).ToList(), roundLength, shuffle);
    }

    public List<OnlineZeeplevelDto> LoadLocalPlaylist(string name)
    {
        if (!PlaylistApi.Exists(name))
        {
            return new List<OnlineZeeplevelDto>();
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(name);
        return playlist.levels.Select(l => new OnlineZeeplevelDto
        {
            UID = l.UID,
            Name = l.Name,
            Author = l.Author,
            WorkshopID = l.WorkshopID,
            Collaborators = l.Collaborators,
            OverrideAuthorName = l.OverrideAuthorName,
            played = l.played
        }).ToList();
    }

    public List<LevelMetadata> LoadLocalPlaylistAsMetadata(string name)
    {
        return LoadLocalPlaylist(name).Select(l => new LevelMetadata
        {
            Uid = l.UID,
            Name = l.Name,
            Author = l.Author,
            WorkshopId = l.WorkshopID
        }).ToList();
    }

    public List<LevelMetadata> GetCurrentZeepkistPlaylist()
    {
        if (ZeepkistNetwork.CurrentLobby == null || ZeepkistNetwork.CurrentLobby.Playlist == null)
        {
            _logger.LogWarning("GetCurrentZeepkistPlaylist: No active lobby or playlist found.");
            return new List<LevelMetadata>();
        }

        return ZeepkistNetwork.CurrentLobby.Playlist.Select(l => new LevelMetadata
        {
            Uid = l.UID,
            Name = l.Name,
            Author = l.Author,
            WorkshopId = l.WorkshopID
        }).ToList();
    }

    public void CreatePlaylist(
        string name,
        List<LevelScriptableObject> levels = null,
        int roundLength = 420,
        bool shuffle = true)
    {
        PlaylistSaveJSON playlistSaveJson = PlaylistApi.CreatePlaylist(name);
        IPlaylistEditor playlistEditor = playlistSaveJson.CreateEditor();

        playlistEditor.Shuffle = shuffle;
        playlistEditor.RoundLength = roundLength;
        if (levels == null)
        {
            playlistEditor.Save();
            return;
        }

        foreach (LevelScriptableObject level in levels)
        {
            playlistEditor.AddLevel(level);
        }

        playlistEditor.Save();
    }

    public void AddLevelToPlaylist(LevelScriptableObject level, string playlistName)
    {
        if (!PlaylistApi.Exists(playlistName))
        {
            _logger.LogError($"Playlist '{playlistName}' does not exist.");
            return;
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(playlistName);
        IPlaylistEditor playlistEditor = playlist.CreateEditor();
        playlistEditor.AddLevel(level);
        playlistEditor.Save();
    }

    public void RemoveLevelFromPlaylist(LevelScriptableObject level, string playlistName)
    {
        RemoveLevelByUid(level.UID, level.name, playlistName);
    }

    public void RemoveLevelFromPlaylist(OnlineZeeplevelDto level, string playlistName)
    {
        RemoveLevelByUid(level.UID, level.Name, playlistName);
    }

    private void RemoveLevelByUid(string uid, string levelName, string playlistName)
    {
        if (!PlaylistApi.Exists(playlistName))
        {
            _logger.LogError($"Playlist '{playlistName}' does not exist.");
            return;
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(playlistName);
        OnlineZeeplevel onlineZeeplevel = playlist.levels.Find(l => l.UID == uid);
        if (onlineZeeplevel == null)
        {
            _logger.LogError($"Level '{levelName}' (UID: {uid}) not found in playlist '{playlistName}'.");
            return;
        }

        playlist.levels.Remove(onlineZeeplevel);
        IPlaylistEditor playlistEditor = playlist.CreateEditor();
        playlistEditor.Save();
    }

    public void DeletePlaylist(string name)
    {
        string[] paths = Directory.GetFiles(_playlistsPath, "*.zeeplist");

        foreach (string path in paths)
        {
            string contents;
            try
            {
                contents = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to read playlist at '{path}': {e.Message}");
                continue;
            }

            try
            {
                PlaylistSaveJSON playlistSaveJson = JsonConvert.DeserializeObject<PlaylistSaveJSON>(contents);
                if (playlistSaveJson != null && playlistSaveJson.name == name)
                {
                    try
                    {
                        File.Delete(path);
                        _logger.LogInfo($"Deleted playlist file: '{path}'");
                        return;
                    }
                    catch (Exception deleteException)
                    {
                        _logger.LogError($"Failed to delete playlist at '{path}': {deleteException.Message}");
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to deserialize playlist at '{path}': {e.Message}");
            }
        }
    }

    /// <summary>Returns all saved playlist names available on disk.</summary>
    public IEnumerable<string> GetAllPlaylistNames()
    {
        return PlaylistApi.GetPlaylists().Select(p => p.name).OrderBy(n => n);
    }

    public List<LevelMetadata> GetPlaylistByName(string name) => LoadLocalPlaylistAsMetadata(name);
}
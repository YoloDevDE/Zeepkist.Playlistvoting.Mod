using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PlaylistVoting.Core.Models;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Playlist;

namespace PlaylistVoting.Infrastructure.Zeepkist;

public class ZeepkistPlaylistService
{
    private readonly string _playlistsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zeepkist", "Playlists");

    public static void SavePlaylist(string name, List<OnlineZeeplevelDto> levels, int roundLength = 360, bool shuffle = false)
    {
        string sanitizedName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.None));
        PlaylistSaveJSON playlistSaveJson = PlaylistApi.CreatePlaylist(sanitizedName);
        IPlaylistEditor playlistEditor = playlistSaveJson.CreateEditor();

        playlistEditor.Shuffle = shuffle;
        playlistEditor.RoundLength = roundLength;

        foreach (OnlineZeeplevel onlineLevel in levels.Select(level => new OnlineZeeplevel
                 {
                     UID = level.UID, Name = level.Name, Author = level.Author, WorkshopID = level.WorkshopID, Collaborators = level.Collaborators, OverrideAuthorName = level.OverrideAuthorName, played = level.played
                 }))
        {
            playlistEditor.AddLevel(onlineLevel);
        }

        playlistEditor.Save();
    }

    public void SavePlaylist(string name, List<LevelMetadata> levels, int roundLength = 360, bool shuffle = false)
    {
        SavePlaylist(name, levels.Select(l => new OnlineZeeplevelDto
        {
            UID = l.Uid, Name = l.Name, Author = l.Author, WorkshopID = l.WorkshopId ?? 0
        }).ToList(), roundLength, shuffle);
    }

    public void UpdateLobbyPlaylist(List<OnlineZeeplevelDto> levels)
    {
        if (ZeepkistNetwork.CurrentLobby == null)
        {
            Logger.Warn("UpdateLobbyPlaylist: No active lobby found.");
            return;
        }

        List<OnlineZeeplevel> onlineLevels = levels.Select(level => new OnlineZeeplevel
        {
            UID = level.UID, Name = level.Name, Author = level.Author, WorkshopID = level.WorkshopID, Collaborators = level.Collaborators, OverrideAuthorName = level.OverrideAuthorName, played = level.played
        }).ToList();

        ZeepkistNetwork.CurrentLobby.Playlist = onlineLevels;

        ZeepkistNetwork.NetworkClient?.SendPacket(new ChangeLobbyPlaylistPacket
        {
            NewTime = ZeepkistNetwork.CurrentLobby.RoundTime, IsRandom = ZeepkistNetwork.CurrentLobby.PlaylistRandom, playlist_all = ZeepkistNetwork.CurrentLobby.Playlist, CurrentIndex = ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex
            , NextIndex = ZeepkistNetwork.CurrentLobby.NextPlaylistIndex
        });

        Logger.Info($"UpdateLobbyPlaylist: Updated lobby playlist with {onlineLevels.Count} levels.");
    }

    public void UpdateLobbyPlaylist(List<LevelMetadata> levels)
    {
        UpdateLobbyPlaylist(levels.Select(l => new OnlineZeeplevelDto
        {
            UID = l.Uid, Name = l.Name, Author = l.Author, WorkshopID = l.WorkshopId ?? 0
        }).ToList());
    }

    public List<OnlineZeeplevelDto> LoadLocalPlaylist(string name)
    {
        string sanitizedName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.None));

        if (!PlaylistApi.Exists(sanitizedName))
        {
            return new List<OnlineZeeplevelDto>();
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(sanitizedName);
        return playlist.levels.Select(l => new OnlineZeeplevelDto
        {
            UID = l.UID, Name = l.Name, Author = l.Author, WorkshopID = l.WorkshopID, Collaborators = l.Collaborators, OverrideAuthorName = l.OverrideAuthorName, played = l.played
        }).ToList();
    }

    public List<LevelMetadata> LoadLocalPlaylistAsMetadata(string name)
    {
        return LoadLocalPlaylist(name).Select(l => new LevelMetadata
        {
            Uid = l.UID, Name = l.Name, Author = l.Author, WorkshopId = l.WorkshopID
        }).ToList();
    }

    public List<LevelMetadata> GetCurrentZeepkistPlaylist()
    {
        if (ZeepkistNetwork.CurrentLobby == null || ZeepkistNetwork.CurrentLobby.Playlist == null)
        {
            Logger.Warn("GetCurrentZeepkistPlaylist: No active lobby or playlist found.");
            return new List<LevelMetadata>();
        }

        return ZeepkistNetwork.CurrentLobby.Playlist.Select(l => new LevelMetadata
        {
            Uid = l.UID, Name = l.Name, Author = l.Author, WorkshopId = l.WorkshopID
        }).ToList();
    }

    public void CreatePlaylist(string name, List<LevelScriptableObject> levels = null, int roundLength = 420, bool shuffle = true)
    {
        string sanitizedName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.None));
        PlaylistSaveJSON playlistSaveJson = PlaylistApi.CreatePlaylist(sanitizedName);
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
        string sanitizedName = string.Join("_", playlistName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.None));

        if (!PlaylistApi.Exists(sanitizedName))
        {
            Logger.Error($"Playlist '{sanitizedName}' does not exist.");
            return;
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(sanitizedName);
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
        string sanitizedName = string.Join("_", playlistName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.None));

        if (!PlaylistApi.Exists(sanitizedName))
        {
            Logger.Error($"Playlist '{sanitizedName}' does not exist.");
            return;
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(sanitizedName);
        OnlineZeeplevel onlineZeeplevel = playlist.levels.Find(l => l.UID == uid);

        if (onlineZeeplevel == null)
        {
            Logger.Error($"Level '{levelName}' (UID: {uid}) not found in playlist '{playlistName}'.");
            return;
        }

        playlist.levels.Remove(onlineZeeplevel);
        IPlaylistEditor playlistEditor = playlist.CreateEditor();
        playlistEditor.Save();
    }

    public void DeletePlaylist(string name)
    {
        if (!Directory.Exists(_playlistsPath))
        {
            Logger.Warn($"DeletePlaylist: Playlists directory does not exist: '{_playlistsPath}'");
            return;
        }

        string sanitizedName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.None));
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
                Logger.Error($"Failed to read playlist at '{path}': {e.Message}");
                continue;
            }

            try
            {
                PlaylistSaveJSON playlistSaveJson = JsonConvert.DeserializeObject<PlaylistSaveJSON>(contents);

                if (playlistSaveJson != null && (playlistSaveJson.name == name || playlistSaveJson.name == sanitizedName))
                {
                    try
                    {
                        File.Delete(path);
                        Logger.Info($"Deleted playlist file: '{path}'");
                        return;
                    }
                    catch (Exception deleteException)
                    {
                        Logger.Error($"Failed to delete playlist at '{path}': {deleteException.Message}");
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to deserialize playlist at '{path}': {e.Message}");
            }
        }
    }

    /// <summary>Returns all saved playlist names available on disk.</summary>
    public IEnumerable<string> GetAllPlaylistNames()
    {
        return PlaylistApi.GetPlaylists().Select(p => p.name).OrderBy(n => n);
    }

    public List<LevelMetadata> GetPlaylistByName(string name) => LoadLocalPlaylistAsMetadata(name);

    public void UploadPlaylistByNameToLobby(string name)
    {
        List<LevelMetadata> levels = GetPlaylistByName(name);

        if (levels != null && levels.Any())
        {
            UpdateLobbyPlaylist(levels);
        }
        else
        {
            Logger.Warn($"UploadPlaylistByNameToLobby: Playlist '{name}' not found or empty.");
        }
    }
}
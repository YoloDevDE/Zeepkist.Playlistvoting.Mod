using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Newtonsoft.Json;
using ZeepkistNetworking;
using ZeepSDK.Playlist;

namespace PlaylistVoting.misc;

public interface IPlaylistService
{
    void CreatePlaylist(string name, List<LevelScriptableObject> levels = null, int roundLength = 420, bool shuffle = true);
    void AddLevelToPlaylist(LevelScriptableObject level, string playlistName);
    void RemoveLevelFromPlaylist(LevelScriptableObject level, string playlistName);
    void RemoveLevelFromPlaylist(OnlineZeeplevel level, string playlistName);
    void DeletePlaylist(string name);
    IEnumerable<string> GetAllPlaylistNames();
}

public class PlaylistService : IPlaylistService
{
    private readonly ManualLogSource _logger;
    private readonly string _playlistsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zeepkist", "Playlists");

    public PlaylistService(ManualLogSource logger)
    {
        _logger = logger;
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
        if (!PlaylistApi.Exists(playlistName))
        {
            _logger.LogError($"Playlist '{playlistName}' does not exist.");
            return;
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(playlistName);
        OnlineZeeplevel onlineZeeplevel = playlist.levels.Find(l => l.UID == level.UID);

        RemoveLevelFromPlaylist(onlineZeeplevel, playlistName);
    }

    public void RemoveLevelFromPlaylist(OnlineZeeplevel level, string playlistName)
    {
        if (!PlaylistApi.Exists(playlistName))
        {
            _logger.LogError($"Playlist '{playlistName}' does not exist.");
            return;
        }

        PlaylistSaveJSON playlist = PlaylistApi.GetPlaylist(playlistName);
        OnlineZeeplevel onlineZeeplevel = playlist.levels.Find(l => l.UID == level.UID);
        if (onlineZeeplevel == null)
        {
            _logger.LogError($"Level '{level.Name}' not found in playlist '{playlistName}'.");
            return;
        }

        playlist.levels.Remove(level);
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
    public IEnumerable<string> GetAllPlaylistNames() => PlaylistApi.GetPlaylists().Select(p => p.name).OrderBy(n => n);

    public void GetPlaylistByName(string name) => PlaylistApi.GetPlaylist(name);
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using BepInEx;
using PlaylistVoting.commands;
using UnityEngine;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.Level;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace PlaylistVoting.states;

public class StateActive : State
{
    private Timer _timer;
    private bool _hasRemindedToVote;
    private int _noVotes;

    private int _yesVotes;

    public StateActive(Plugin plugin) : base(plugin) { }

    public bool IsRacing { get; set; } = true;

    public override void Enter()
    {
        VoteYes.OnHandle += HandleRequestAsyncYes;
        VoteNo.OnHandle += HandleRequestAsyncNo;
        RacingApi.LevelLoaded += OnLevelLoaded;
        MultiplayerApi.DisconnectedFromGame += OnVoteStopOnOnHandle;
        VoteStop.OnHandle += OnVoteStopOnOnHandle;
        VoteStart.OnHandle += OnVoteStartOnOnHandle;
        ZeepkistNetwork.MasterChanged += OnMasterChanged;
        RacingApi.RoundEnded += OnRoundEnded;

        StartTimer();
        if (Plugin.Instance.uid != LevelApi.CurrentLevel.UID)
        {
            Plugin.HandleRequestAsyncReset(false);
        }
    }

    private void OnRoundEnded()
    {
        IsRacing = false;
    }

    private void OnMasterChanged(ZeepkistNetworkPlayer obj)
    {
        MessengerApi.LogWarning("Voting stopped because the host changed!");
        Plugin.SwitchState(new StateInactive(Plugin));
    }

    private void OnVoteStartOnOnHandle()
    {
        MessengerApi.LogWarning("Vote is already running!");
    }

    private void OnVoteStopOnOnHandle()
    {
        MessengerApi.LogSuccess("Vote successfully stopped!");
        Plugin.SwitchState(new StateInactive(Plugin));
    }

    public override void Exit()
    {
        VoteYes.OnHandle -= HandleRequestAsyncYes;
        VoteNo.OnHandle -= HandleRequestAsyncNo;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        MultiplayerApi.DisconnectedFromGame -= OnVoteStopOnOnHandle;
        VoteStop.OnHandle -= OnVoteStopOnOnHandle;
        VoteStart.OnHandle -= OnVoteStartOnOnHandle;
        ZeepkistNetwork.MasterChanged -= OnMasterChanged;
        RacingApi.RoundEnded -= OnRoundEnded;
        StopTimer();
    }

    public void DeleteVotedLevelFromPlaylistAndDoMoreThingsCauseItsCool()
    {
        List<OnlineZeeplevel> newPlaylist = ZeepkistNetwork.CurrentLobby.Playlist.Where(level => !level.UID.Equals(Plugin.Instance.uid)).ToList();

        ZeepkistNetwork.CurrentLobby.Playlist.Clear();
        ZeepkistNetwork.CurrentLobby.Playlist.AddRange(newPlaylist);
        ZeepkistNetwork.CurrentLobby.PlaylistRandom = false;
        ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex = ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex > 0 ? ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex - 1 : 0;
        ZeepkistNetwork.CurrentLobby.NextPlaylistIndex = ZeepkistNetwork.CurrentLobby.NextPlaylistIndex < ZeepkistNetwork.CurrentLobby.Playlist.Count ? ZeepkistNetwork.CurrentLobby.NextPlaylistIndex - 1 : 0;

        ZeepkistNetwork.SendLobbyPlaylistToServer(ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex, ZeepkistNetwork.CurrentLobby.NextPlaylistIndex);
    }

    private void OnLevelLoaded()
    {
        IsRacing = true;
        Plugin.StartCoroutine(DelayedLevelCheck());
    }

    private IEnumerator DelayedLevelCheck()
    {
        const int delay = 3;
        if (_noVotes >= _yesVotes && Plugin.Instance.DeleteNoLevels && ZeepkistNetwork.CurrentLobby.Playlist.Count > 1)
        {
            MessengerApi.LogWarning("Deleting the level from the playlist because it was rejected by the vote.", delay);
            yield return new WaitForSeconds(delay);
            if (ZeepkistNetwork.CurrentLobby.Playlist.Any(l => l.UID.Equals(Plugin.Instance.uid)))
            {
                DeleteVotedLevelFromPlaylistAndDoMoreThingsCauseItsCool();
                if (ZeepkistNetwork.CurrentLobby.Playlist.Any(l => l.UID.Equals(Plugin.Instance.uid)))
                {
                    MessengerApi.LogWarning("Failed to delete the level from the playlist!", 5f);
                }
                else
                {
                    MessengerApi.Log($"'{Plugin.Instance.level} by {Plugin.Instance.author}' was deleted from the playlist because it was rejected by the vote.");
                }
            }
            else
            {
                MessengerApi.LogWarning($"Failed to delete '{Plugin.Instance.level} by {Plugin.Instance.author}' because it does not exist in the current playlist!", 5f);
            }
        }

        Plugin.HandleRequestAsyncReset();
        StartTimer();
    }


    public void StartTimer()
    {
        _hasRemindedToVote = false;
        _timer = new Timer(1000);
        _timer.Elapsed += HandleRequestAsyncGet;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public async Task HandleRequestAsync(string url)
    {
        string[] timeLeft = ZeepkistNetwork.CurrentLobby.timeLeftString.Split(":");

        if (timeLeft[0] == "00" && int.Parse(timeLeft[1]) <= 30 && !_hasRemindedToVote)
            // if (ZeepkistNetwork.CurrentLobby.timeLeftString <= 30000 && !HasRemindedToVote)
        {
            ZeepkistNetwork.SendCustomChatMessage(true, 0,
                "<br><color=#f0f0f0>REMEMBER TO <b>VOTE</b> GUYS!<br>Type <color=#00FF00><b>!y</b></color> in the chat to get this Map into the playlist<br>Type <color=#FF0000><b>!n</b></color> if you don't want it in the Playlist<br>----------------</color>",
                Plugin.Instance.ServermessageTitle);
            _hasRemindedToVote = true;
        }

        try
        {
            using HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                Match match = Regex.Match(content, @"Current Total -> (\d+)/(\d+) \(y/n\)");


                if (match.Success)
                {
                    if (ZeepkistNetwork.CurrentLobby.GameState != 0)
                    {
                        StopTimer();
                        return;
                    }

                    _yesVotes = int.Parse(match.Groups[1].Value);
                    _noVotes = int.Parse(match.Groups[2].Value);

                    string emote;

                    if (_yesVotes > _noVotes)
                    {
                        emote = Plugin.Instance.WinEmote;
                    }
                    else if (_yesVotes < _noVotes)
                    {
                        emote = Plugin.Instance.LoseEmote;
                    }
                    else
                    {
                        emote = Plugin.Instance.TieEmote;
                    }

                    string message = "Current Total -> %y/%n (y/n) %e";
                    message = message
                              .Replace("Current Total ->", $"<b><u>{Plugin.Instance.ServermessageTitle}</u></b><br><#ff9900>{Plugin.Instance.level} <#ffffff>by <#ff9900>{Plugin.Instance.author}<#ffffff><br>Votes: ")
                              .Replace("%y", $"<#00aa00>{_yesVotes.ToString()}<#ffffff>")
                              .Replace("%n", $"<#aa0000>{_noVotes.ToString()}<#ffffff>")
                              .Replace("%e", emote)
                              .Replace("%l", Plugin.level)
                              .Replace("%a", Plugin.author);
                    if (message.IsNullOrWhiteSpace())
                    {
                        message = "No Message set. Please do so.";
                    }

                    if (IsRacing)
                    {
                        ChatApi.SendMessage(
                            $"/servermessage white 0 <align=\"left\"><size=\"30%\">{message}<br><#ffffff></size><size=\"20%\"><voffset=-0.5em>Type !y in the chat if you like the current level</voffset><br>Type !n in the chat if not");
                    }
                }
                else
                {
                    MessengerApi.LogError("Error in Voting System");
                }
            }
        }
        catch (Exception ex)
        {
            // Handle exception
            ChatApi.SendMessage($"Error: {ex.Message}");
        }
    }

    public void StopTimer()
    {
        _timer.Stop();
        _timer.Elapsed -= HandleRequestAsyncGet;
        _timer.Dispose();
        if (IsRacing)
        {
            ChatApi.SendMessage("/servermessage remove");
        }
    }

    public void HandleRequestAsyncNo(ulong playerId)
    {
        ZeepkistNetwork.SendCustomChatMessage(false, playerId, "you voted 'no'", Plugin.ServermessageTitle);
        string url =
            $"https://yololurk.herokuapp.com/api/ronan/no?token={Plugin.WebToken}&twitchUser={playerId}";

        _ = HandleRequestAsync(url);
    }

    public void HandleRequestAsyncYes(ulong playerId)
    {
        ZeepkistNetwork.SendCustomChatMessage(false, playerId, "you voted 'yes'", Plugin.ServermessageTitle);
        string url =
            $"https://yololurk.herokuapp.com/api/ronan/yes?token={Plugin.WebToken}&twitchUser={playerId}";

        _ = HandleRequestAsync(url);
    }

    public void HandleRequestAsyncGet(object sender, ElapsedEventArgs elapsedEventArgs)
    {
        string url =
            $"https://yololurk.herokuapp.com/api/ronan/get/total?token={Plugin.WebToken}";
        _ = HandleRequestAsync(url);
    }
}
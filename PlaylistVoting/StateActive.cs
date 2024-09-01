using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using BepInEx;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.Level;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace PlaylistVoting;

public class StateActive : State
{
    private Timer _timer;
    private bool HasRemindedToVote;

    public StateActive(Plugin plugin) : base(plugin)
    {
    }

    public override void Enter()
    {
        VoteYes.OnHandle += HandleRequestAsyncYes;
        VoteNo.OnHandle += HandleRequestAsyncNo;
        RacingApi.LevelLoaded += OnLevelLoaded;
        MultiplayerApi.DisconnectedFromGame += OnVoteStopOnOnHandle;
        VoteStop.OnHandle += OnVoteStopOnOnHandle;
        VoteStart.OnHandle += OnVoteStartOnOnHandle;

        StartTimer();
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
        StopTimer();
    }

    private void OnLevelLoaded()
    {
        Plugin.HandleRequestAsyncReset();
        StartTimer();
    }


    public void StartTimer()
    {
        HasRemindedToVote = false;
        _timer = new Timer(1000);
        _timer.Elapsed += HandleRequestAsyncGet;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public async Task HandleRequestAsync(string url)
    {
        string[] timeLeft = ZeepkistNetwork.CurrentLobby.timeLeftString.Split(":");

        if (timeLeft[0] == "00" && int.Parse(timeLeft[1]) <= 30 && !HasRemindedToVote)
            // if (ZeepkistNetwork.CurrentLobby.timeLeftString <= 30000 && !HasRemindedToVote)
        {
            ChatApi.SendMessage(
                "<br>REMEMBER TO VOTE GUYS!<br>Type !y in the chat to get this Map into the playlist<br>Type !n if you don't want it in the Playlist");
            HasRemindedToVote = true;
        }

        try
        {
            using (HttpClient httpClient = new HttpClient())
            {
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

                        int yesVotes = int.Parse(match.Groups[1].Value);
                        int noVotes = int.Parse(match.Groups[2].Value);

                        string color, emote;

                        if (yesVotes > noVotes)
                        {
                            color = Plugin.Instance.WinColor;
                            emote = Plugin.Instance.WinEmote;
                        }
                        else if (yesVotes < noVotes)
                        {
                            color = Plugin.Instance.LoseColor;
                            emote = Plugin.Instance.LoseEmote;
                        }
                        else
                        {
                            color = Plugin.Instance.TieColor;
                            emote = Plugin.Instance.TieEmote;
                        }

                        string message = Plugin.Instance.MessageFormat
                            .Replace("Current Total ->", $"<b><u>Playlist-Voting</u></b><br><#ff9900>{LevelApi.CurrentLevel.Name} <#ffffff>by <#ff9900>{LevelApi.CurrentLevel.Author}<#ffffff><br>Votes: ")
                            .Replace("%y", $"<#00aa00>{yesVotes.ToString()}<#ffffff>")
                            .Replace("%n", $"<#aa0000>{noVotes.ToString()}<#ffffff>")
                            .Replace("%e", emote)
                            .Replace("%l", Plugin.level)
                            .Replace("%a", Plugin.author);
                        if (message.IsNullOrWhiteSpace())
                        {
                            message = "No Message set. Please do so.";
                        }

                        color = color.ToLower();
                        ChatApi.SendMessage($"/servermessage white 0 <align=\"left\"><size=\"30%\"><br><br>{message}<br><#ffffff></size><size=\"20%\"><voffset=-0.5em>Type !y in the chat if you like the current level</voffset><br>Type !n in the chat if not");
                    }
                    else
                    {
                        MessengerApi.LogError("Error in Voting System");
                    }
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
        ChatApi.SendMessage("/servermessage remove");
    }

    public void HandleRequestAsyncNo(ulong playerId)
    {
        string url =
            $"https://yololurk.herokuapp.com/api/ronan/no?token={Plugin.WebToken}&twitchUser={playerId}";

        HandleRequestAsync(url);
    }

    public void HandleRequestAsyncYes(ulong playerId)
    {
        string url =
            $"https://yololurk.herokuapp.com/api/ronan/yes?token={Plugin.WebToken}&twitchUser={playerId}";

        HandleRequestAsync(url);
    }

    public void HandleRequestAsyncGet(object sender, ElapsedEventArgs elapsedEventArgs)
    {
        string url =
            $"https://yololurk.herokuapp.com/api/ronan/get/total?token={Plugin.WebToken}";
        HandleRequestAsync(url);
    }
}
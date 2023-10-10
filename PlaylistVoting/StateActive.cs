using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.Purchasing;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace PlaylistVoting;

public class StateActive : State
{
    private Timer _timer;


    public StateActive(Plugin plugin) : base(plugin)
    {
    }

    public override void Enter()
    {
        VoteYes.OnHandle += HandleRequestAsyncYes;
        VoteNo.OnHandle += HandleRequestAsyncNo;
        RacingApi.LevelLoaded += OnPlayerSpawned;
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
        RacingApi.LevelLoaded -= OnPlayerSpawned;
        MultiplayerApi.DisconnectedFromGame -= OnVoteStopOnOnHandle;
        VoteStop.OnHandle -= OnVoteStopOnOnHandle;
        VoteStart.OnHandle -= OnVoteStartOnOnHandle;
        StopTimer();
    }

    private void OnPlayerSpawned()
    {
        StartTimer();
    }


    public void StartTimer()
    {
        _timer = new Timer(1000);
        _timer.Elapsed += HandleRequestAsyncGet;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public async Task HandleRequestAsync(string url)
    {


        try
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var match = Regex.Match(content, @"Current Total -> (\d+)/(\d+) \(y/n\)");

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
                            .Replace("%y", yesVotes.ToString())
                            .Replace("%n", noVotes.ToString())
                            .Replace("%e", emote)
                            .Replace("%l", Plugin.level)
                            .Replace("%a", Plugin.author);
                        if (message.IsNullOrWhiteSpace())
                            message = "No Message set. Please do so.";
                        color = color.ToLower();

                        ChatApi.SendMessage($"/servermessage {color} 0 {message}");
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
        var url =
            $"https://yololurk.herokuapp.com/api/ronan/no?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991&twitchUser={playerId}";

        HandleRequestAsync(url);
    }

    public void HandleRequestAsyncYes(ulong playerId)
    {
        var url =
            $"https://yololurk.herokuapp.com/api/ronan/yes?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991&twitchUser={playerId}";

        HandleRequestAsync(url);
    }

    public void HandleRequestAsyncGet(object sender, ElapsedEventArgs elapsedEventArgs)
    {
        var url =
            "https://yololurk.herokuapp.com/api/ronan/get/total?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991";
        HandleRequestAsync(url);
    }
}
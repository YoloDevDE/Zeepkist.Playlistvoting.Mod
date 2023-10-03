using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Timers;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;
using ZeepSDK.Racing;

namespace PlaylistVoting;

public class StateActive : State
{
    private Timer _updateTimer;

    public override void Enter()
    {
        VoteYes.OnHandle += HandleRequestAsyncYes;
        VoteNo.OnHandle += HandleRequestAsyncNo;
        RacingApi.LevelLoaded += OnPlayerSpawned;
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
        _updateTimer = new Timer(2500); // Setze das Intervall auf 2,5 Sekunden
        _updateTimer.Elapsed += (sender, e) => HandleRequestAsyncGet(
            0); // Hier setze ich die playerId auf 0, da sie in HandleRequestAsyncGet nicht verwendet wird. Du kannst den Wert ändern, falls notwendig.
        _updateTimer.Start();
        HandleRequestAsyncGet(0);
    }

    public async void HandleRequestAsync(string url)
    {
        
        if (ZeepkistNetwork.CurrentLobby.GameState != 0)
        {
            StopTimer();
            return;
        }
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
                        int yesVotes = int.Parse(match.Groups[1].Value);
                        int noVotes = int.Parse(match.Groups[2].Value);
                        string color = yesVotes > noVotes ? "green" : "red";

                        ChatApi.SendMessage($"/servermessage {color} 0 Current Total -> {yesVotes}/{noVotes} (y/n)");
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
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
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

    public void HandleRequestAsyncGet(ulong playerId)
    {
        var url =
            $"https://yololurk.herokuapp.com/api/ronan/get?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991";
        HandleRequestAsync(url);
    }

    public StateActive(Plugin plugin) : base(plugin)
    {
    }
}
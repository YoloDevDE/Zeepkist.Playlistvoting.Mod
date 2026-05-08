using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaylistVoting.Commands.Local;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.UI;
using PlaylistVoting.Infrastructure.Zeepkist;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Playlist;
using ToastNotification = YoloDev.Zeepkist.ToastNotification;

namespace PlaylistVoting.Core.State;

public class VotingActiveState : VotingStateBase
{
    private readonly VotingController _controller;
    private bool _hasRemindedToVote;

    private VotingResultResponse _lastResult;
    

    private string _lastTimeSent;

    public VotingActiveState(VotingController controller) : base(controller)
    {
        _controller = controller;
    }

    private static int VoteReminderThresholdSeconds => VotingConfig.Instance.VoteReminderThreshold;

    public override void OnEnter()
    {
        _hasRemindedToVote = false;

        _ = StartRacingPhaseAsync();
        VotingChatManager.RegisterRemoteCommands();
    }

    public override void OnExit()
    {
        _controller.BackendService.Disconnect();
        if (IsRacing())
        {
            MessageApi.RemoveServerMessage();
        }

        VotingChatManager.UnregisterRemoteCommands();
    }

    public override void OnUpdate()
    {
        SendVoteReminderIfNeeded();
        SendTimerIfChanged();
    }


    public override void OnMasterStatusChanged()
    {
        _controller.TransitionTo(new VotingDisabledState(_controller));
    }

    public override void OnVotingResultReceived(VotingResultResponse result)
    {
        
        
        if (result?.Votes != null)
        {
            // Only update level metadata if the backend actually provided it to avoid "Unknown"
            if (result.Level != null && !string.IsNullOrEmpty(result.Level.Uid))
            {
                Controller.CurrentLevel = result.Level;
            }

            _lastResult = result;
            RefreshDisplay();
        }
    }

    private bool IsRacing() => _controller.CurrentLobbyState == ZeepkistLobbyState.Racing;

    private void RefreshDisplay()
    {
        if (!IsRacing() || _lastResult == null)
        {
            return;
        }

        string message = VotingDisplayManager.BuildVoteDisplayMessage(
            _controller.ServermessageTitle,
            _lastResult.SessionName,
            _controller.CurrentLevel,
            _lastResult.Votes,
            _controller.BackendService.IsConnected);

        VotingDisplayManager.SendVotingUpdate(message);
    }

    public override void OnLobbyStateChanged(ZeepkistLobbyState state)
    {
        if (state == ZeepkistLobbyState.NotInALobby)
        {
            _controller.TransitionTo(new VotingDisabledState(_controller));
        }
    }

    public override void OnPlayerVoted(ulong steamId, VotingType type)
    {
        _ = HandleVoteAsync(steamId, type);
    }

    public override void OnLevelLoaded()
    {
        _ = ShowResultsAsync();
    }

    public override void OnVoteStartRequested()
    {
        string msg = new TMPRichTextBuilder("Already running.")
                     .Break()
                     .AddLayer("If you intended to restart, type ")
                     .AddLayer($"{new VoteRestart().Prefix}{new VoteRestart().Command}", b => b.Color("#f00"))
                     .Build();
        ToastNotification.Warning(msg, 5f);
    }

    public override void OnVoteStopRequested()
    {
        _controller.TransitionTo(new VotingDisabledState(_controller));
    }

    public override void OnVoteRestartRequested()
    {
        OnVoteStopRequested();
        _controller.TransitionTo(new VotingActiveState(_controller));
    }

    private async Task StartRacingPhaseAsync()
    {
        try
        {
            if (ZeepkistNetwork.LocalPlayer != null)
            {
                await _controller.BackendService.ConnectAsync(ZeepkistNetwork.LocalPlayer.SteamID.ToString());
            }

            // Capture current level metadata from game
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

            _controller.CurrentLevel = level;

            await _controller.BackendService.SetCurrentLevelAsync(
                _controller.CurrentLevel,
                VotingConfig.Instance.IncludeAbstainVotes);

            VotingResultResponse initial = await _controller.BackendService.FetchVotesAsync();
            if (initial != null)
            {
                _controller.UpdateFromVotingResult(initial);
            }
        }
        catch (Exception ex)
        {
            _controller.Logger.LogError($"Error starting race phase: {ex.Message}");
            _controller.TransitionTo(new VotingDisabledState(_controller));
        }

        string activeMsg = new TMPRichTextBuilder("Playlist voting is now ")
                           .AddLayer("ACTIVE", b => b.Color("#00FF00"))
                           .AddLayer(".")
                           .Build();

        ZeepkistNetwork.SendCustomChatMessage(true, 0, activeMsg, _controller.ServermessageTitle);
        ToastNotification.Success("Started successfully!");
    }

    private async Task ShowResultsAsync()
    {
        try
        {
            string result = await _controller.BackendService.ResetVotesAsync();
            string msg = new TMPRichTextBuilder(result)
                         .Break()
                         .AddLayer("----------------")
                         .Color("#f0f0f0")
                         .Build();

            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                msg,
                _controller.ServermessageTitle);
        }
        catch (Exception ex)
        {
            _controller.Logger.LogError($"Error showing results: {ex.Message}");
        }
    }

    private async Task HandleVoteAsync(ulong steamId, VotingType type)
    {
        try
        {
            VotingResultResponse result = await _controller.BackendService.SubmitVoteAsync(steamId, type);
            if (result == null)
            {
                return;
            }

            string msg = type switch
            {
                VotingType.Yes => "You voted 'yes'.",
                VotingType.No => "You voted 'no'.",
                VotingType.Abstain => "You voted 'abstain'.",
                VotingType.Remove => "Your vote was removed.",
                _ => "Vote submitted."
            };

            ZeepkistNetwork.SendCustomChatMessage(false, steamId, msg, _controller.ServermessageTitle);
            _controller.UpdateFromVotingResult(result);
        }
        catch (Exception ex)
        {
            _controller.Logger.LogError($"Error handling vote: {ex.Message}");
        }
    }

    private void SendTimerIfChanged()
    {
        string currentTime = ZeepkistNetwork.CurrentLobby?.timeLeftString ?? "--:--";
        if (currentTime != _lastTimeSent)
        {
            _controller.BackendService.SendTimer(currentTime);
            _lastTimeSent = currentTime;
        }
    }

    private void SendVoteReminderIfNeeded()
    {
        if (_hasRemindedToVote || ZeepkistNetwork.CurrentLobby == null)
        {
            return;
        }

        string[] parts = ZeepkistNetwork.CurrentLobby.timeLeftString.Split(':');
        if (parts.Length < 2)
        {
            return;
        }

        if (parts[0] == "00" && int.TryParse(parts[1], out int secs) && secs <= VoteReminderThresholdSeconds)
        {
            string reminderMsg = new TMPRichTextBuilder()
                                 .Break()
                                 .AddLayer("LAST CHANCE TO ", b => b.Underline())
                                 .AddLayer("VOTE", b => b.Underline().Bold())
                                 .AddLayer("!", b => b.Underline())
                                 .Break()
                                 .AddLayer("Type ")
                                 .AddLayer("!y", b => b.Color("#00FF00").Bold())
                                 .AddLayer(" to ")
                                 .AddLayer("keep", b => b.Bold())
                                 .AddLayer(" this level in the playlist")
                                 .Break()
                                 .AddLayer("Type ")
                                 .AddLayer("!n", b => b.Color("#FF0000").Bold())
                                 .AddLayer(" to remove it")
                                 .Break()
                                 .Color("#f0f0f0")
                                 .Build();

            ZeepkistNetwork.SendCustomChatMessage(
                true, 0,
                reminderMsg,
                _controller.ServermessageTitle);
            _hasRemindedToVote = true;
        }
    }
}
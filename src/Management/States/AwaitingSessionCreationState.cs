using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States;

public class AwaitingSessionCreationState : VotingStateBase
{
    private readonly List<LevelMetadata> _playlist;
    private readonly string _playlistName;

    public AwaitingSessionCreationState(VotingController controller, string requestedSessionName = null) : base(controller)
    {
        _playlist = ZeepkistPlaylistService.Instance.GetCurrentZeepkistPlaylist();
        _playlistName = string.IsNullOrEmpty(requestedSessionName) ? "Playlistvoting" : requestedSessionName;
    }

    public override void OnEnter()
    {
        Controller.OverlayService.Clear();

        string msg = new RichText().Append("No active session found.", b => b.Color("#FFFF00")).Break().Append("Do you want to create a new session?").Break().Append("Name: ", b => b.Size(80)).Append(_playlistName, b => b.Color("#FFFFFF").Bold()).Break()
                                   .Append("Levels: ", b => b.Size(80)).Append(_playlist.Count.ToString(), b => b.Color("#FFFFFF").Bold()).Break().Append("Use ").Append("/vote confirm", b => b.Color("#00FF00")).Append(" to proceed or ")
                                   .Append("/vote stop", b => b.Color("#FF0000")).Append(" to cancel.").Build();

        ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
    }

    public override void OnConfirmRequested()
    {
        _ = CreateSessionAsync();
    }

    public override void OnVoteStopRequested()
    {
        Controller.TransitionTo(new VotingDisabledState(Controller));
    }

    private async Task CreateSessionAsync()
    {
        try
        {
            ToastNotification.Info("Creating session...");
            PlaylistSessionInfo session = await Controller.BackendService.CreateSessionAsync(_playlistName, _playlist);

            if (session == null)
            {
                Logger.Error("Failed to create session.");
                ToastNotification.Error("Failed to create session.");
                return;
            }

            ToastNotification.Success($"Session '{session.DisplayName}' created!");
            Controller.CurrentSession = session;

            // Re-initialize to handle the new session
            Controller.TransitionTo(new InitState(Controller));
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Error("AwaitingSessionCreationState: Token is invalid.");
            string msg = new RichText().Append("The stored token doesn't match any account. Create a new one with ", b => b.Color("#FF0000")).Append("/vote login", b => b.Color("#FFFF00")).Append(" or paste a valid token.", b => b.Color("#FF0000"))
                                       .Build();
            ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
            Controller.TransitionTo(new VotingDisabledState(Controller));
        }
        catch (Exception ex)
        {
            Logger.Error($"Error creating session: {ex.Message}");
            ToastNotification.Error($"Error creating session: {ex.Message}");
        }
    }
}
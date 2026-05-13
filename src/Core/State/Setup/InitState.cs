using System;
using System.Threading.Tasks;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Core.State.Active;
using PlaylistVoting.Core.State.Inactive;
using PlaylistVoting.Infrastructure.UI;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Core.State.Setup;

public class InitState : VotingStateBase
{
    private readonly string _requestedSessionName;

    public InitState(VotingController controller, string requestedSessionName = null) : base(controller)
    {
        _requestedSessionName = requestedSessionName;
    }

    public override void OnEnter()
    {
        Controller.ResetSession();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        VotingConfig.Instance.Reload();

        try
        {
            if (!await Controller.BackendService.CheckHealthAsync())
            {
                Logger.Error("Playlist Voting backend is down.");
                ToastNotification.Error("Playlist Voting backend is down.");
                Controller.TransitionTo(new VotingDisabledState(Controller));
                return;
            }

            if (!await Controller.BackendService.ValidateTokenAsync())
            {
                Logger.Error("InitState: Token is invalid.");
                string msg = new RichText().Append("The stored token doesn't match any account. Create a new one with ", b => b.Color("#FF8800")).Append("/vote login", b => b.Color("#FFFF00")).Append(" or paste a valid token.", b => b.Color("#FF8800"))
                                           .Build();
                ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
                Controller.TransitionTo(new VotingDisabledState(Controller));
                return;
            }

            PlaylistSessionInfo session = await Controller.BackendService.GetActiveSessionAsync();

            if (session == null)
            {
                session = await Controller.BackendService.GetLatestActiveSessionAsync();
            }

            if (session == null)
            {
                Controller.TransitionTo(new AwaitingSessionCreationState(Controller, _requestedSessionName));
                return;
            }

            ToastNotification.Info($"Playlist Voting: Session '{session.DisplayName}' found!");
            Controller.CurrentSession = session;

            string summary = VotingDisplayManager.BuildSettingsSummary(VotingConfig.Instance);
            ZeepkistNetworkHelper.SendLocalPrivateMessage(summary);

            // We have a session!
            if (session.PlaylistModeEnabled && session.HasPlaylist)
            {
                Controller.TransitionTo(new PlaylistStartupState(Controller, session));
            }
            else
            {
                await HandleStartupModeAsync(session);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Error("InitState: Token is invalid.");
            string msg = new RichText().Append("The stored token doesn't match any account. Create a new one with ", b => b.Color("#FF0000")).Append("/vote login", b => b.Color("#FFFF00")).Append(" or paste a valid token.", b => b.Color("#FF0000"))
                                       .Build();
            ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
            Controller.TransitionTo(new VotingDisabledState(Controller));
        }
        catch (Exception ex)
        {
            Logger.Error($"InitState: An error occurred during initialization: {ex.Message}");
            string msg = new RichText().Append("An error occurred during initialization. ", b => b.Color("#FF0000")).Break().Append(ex.Message, b => b.Size(80)).Build();
            ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
            Controller.TransitionTo(new VotingDisabledState(Controller));
        }
    }

    private async Task HandleStartupModeAsync(PlaylistSessionInfo session)
    {
        switch (VotingConfig.Instance.StartupMode)
        {
            case PlaylistVotingStartupMode.AlwaysPlaylistMode:
                await Controller.BackendService.SetPlaylistModeAsync(true);
                Controller.TransitionTo(new PlaylistStartupState(Controller, session));
                break;
            case PlaylistVotingStartupMode.AlwaysSimpleMode:
                await Controller.BackendService.SetPlaylistModeAsync(false);
                Controller.TransitionTo(new SimpleVotingActiveState(Controller, session));
                break;
            case PlaylistVotingStartupMode.AlwaysAsk:
            default:
                Controller.TransitionTo(new AwaitingModeSelectionState(Controller, session));
                break;
        }
    }
}
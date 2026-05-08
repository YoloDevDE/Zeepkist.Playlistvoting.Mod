using System.Threading.Tasks;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Zeepkist;

namespace PlaylistVoting.Core.State;

public class InitState : VotingStateBase
{
    public InitState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        PlaylistSessionInfo session = await Controller.BackendService.GetActiveSessionAsync();
        if (session == null)
        {
            session = await Controller.BackendService.GetLatestActiveSessionAsync();
        }

        if (session == null)
        {
            Controller.TransitionTo(new NoSessionState(Controller));
            return;
        }

        ToastNotification.Info($"Playlist Voting: Session '{session.DisplayName}' found!");
        Controller.CurrentSession = session;

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
using System.Threading.Tasks;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;

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
            session = await Controller.BackendService.GetLatestResumableSessionAsync();
        }

        if (session == null)
        {
            Controller.TransitionTo(new NoSessionState(Controller));
            return;
        }

        Controller.CurrentSession = session;

        // We have a session!
        if (session.PlaylistModeEnabled && session.HasOnlinePlaylist)
        {
            Controller.TransitionTo(new PlaylistStartupState(Controller, session));
        }
        else
        {
            switch (VotingConfig.Instance.StartupMode)
            {
                case PlaylistVotingStartupMode.AlwaysPlaylistMode:
                    Controller.TransitionTo(new PlaylistStartupState(Controller, session));
                    break;
                case PlaylistVotingStartupMode.AlwaysSimpleMode:
                    Controller.TransitionTo(new SimpleVotingActiveState(Controller, session));
                    break;
                case PlaylistVotingStartupMode.AlwaysAsk:
                default:
                    Controller.TransitionTo(new AwaitingModeSelectionState(Controller, session));
                    break;
            }
        }
    }
}
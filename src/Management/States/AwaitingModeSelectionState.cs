using System.Threading.Tasks;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Management.States.Abstractions;
using PlaylistVoting.Networking.Zeepkist;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Management.States;

public class AwaitingModeSelectionState : SessionState
{
    public AwaitingModeSelectionState(VotingController controller, PlaylistSessionInfo session) : base(controller, session) { }

    public override void OnEnter()
    {
        string msg = new RichText().Append("How do you want to start Playlist Voting?", b => b.Bold().Color("#00f8ad")).Break().Append("/vote playlistmode", b => b.Color("#ff9900")).Append(" - automated playlist voting").Break()
                                   .Append("/vote simplemode", b => b.Color("#ff9900")).Append(" - result broadcast only").Build();

        ZeepkistNetworkHelper.SendLocalPrivateMessage(msg);
    }

    public override void OnPlaylistModeRequested()
    {
        ToastNotification.Info("Switching to Playlist Mode...");
        _ = StartPlaylistModeAsync();
    }

    private async Task StartPlaylistModeAsync()
    {
        await Controller.BackendService.SetPlaylistModeAsync(true);
        Controller.TransitionTo(new PlaylistStartupState(Controller, Session));
    }

    public override void OnSimpleModeRequested()
    {
        ToastNotification.Info("Switching to Simple Mode...");
        _ = StartSimpleModeAsync();
    }

    private async Task StartSimpleModeAsync()
    {
        await Controller.BackendService.SetPlaylistModeAsync(false);
        Controller.TransitionTo(new SimpleVotingActiveState(Controller, Session));
    }
}
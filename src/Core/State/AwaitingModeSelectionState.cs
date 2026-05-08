using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Core.State.Abstractions;
using YoloDev.Text;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State;

public class AwaitingModeSelectionState : SessionState
{
    public AwaitingModeSelectionState(VotingController controller, PlaylistSessionInfo session) : base(controller, session) { }

    public override void OnEnter()
    {
        string msg = new TMPRichTextBuilder()
                     .AddLayer("How do you want to start Playlist Voting?", b => b.Bold().Color("#00f8ad"))
                     .Break()
                     .AddLayer("/vote playlistmode", b => b.Color("#ff9900"))
                     .AddLayer(" - automated playlist voting")
                     .Break()
                     .AddLayer("/vote simplemode", b => b.Color("#ff9900"))
                     .AddLayer(" - result broadcast only")
                     .Build();

        if (ZeepkistNetwork.LocalPlayer != null)
        {
            MessageApi.SendPrivateCustomChatMessage(msg, "VOTING", ZeepkistNetwork.LocalPlayer.SteamID);
        }
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
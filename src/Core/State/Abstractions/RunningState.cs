using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using PlaylistVoting.Infrastructure.Zeepkist;
using YoloDev.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Core.State.Abstractions;

public abstract class RunningState : SessionState
{
    protected RunningState(VotingController controller, PlaylistSessionInfo session) : base(controller, session) { }

    public sealed override void OnEnter()
    {
        _ = Controller.BackendService.ConnectAsync(ZeepkistNetwork.LocalPlayer?.SteamID.ToString() ?? "");
        VotingChatManager.RegisterRemoteCommands();
        OnRunningEnter();
    }

    public sealed override void OnExit()
    {
        OnRunningExit();
        Controller.BackendService.Disconnect();
        MessageApi.RemoveServerMessage();
        VotingChatManager.UnregisterRemoteCommands();
    }

    protected virtual void OnRunningEnter() { }
    protected virtual void OnRunningExit() { }
}

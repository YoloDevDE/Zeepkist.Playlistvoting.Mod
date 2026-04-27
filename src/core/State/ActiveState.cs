using System.Threading;
using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using ZeepkistClient;
using ZeepSDK.Chat;

namespace PlaylistVoting.Core.State;

public class ActiveState(VotingController controller) : VotingStateBase
{
    private CancellationTokenSource _stateCts;

    public override bool RequiresWebSocket => true;

    public override async Task OnEnterAsync(CancellationToken ct)
    {
        controller.Logger.LogInfo("Mod is now Active. Starting voting cycle...");
        _stateCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await controller.EnsureLoggedInAsync(_stateCts.Token);
        await controller.ResetAndStartNewVotingAsync(_stateCts.Token);

        controller.StartVotingCycle();
        _ = controller.FetchAndDisplayVotesAsync(_stateCts.Token);

        ZeepkistNetwork.SendCustomChatMessage(true, 0, "Playlist voting is now <#00FF00>ACTIVE</color>.", controller.ServermessageTitle);
    }

    public override Task OnExitAsync()
    {
        _stateCts?.Cancel();
        _stateCts?.Dispose();
        ChatApi.SendMessage("/servermessage remove");
        return Task.CompletedTask;
    }

    public override void OnUpdate()
    {
        controller.SendVoteReminderIfNeeded();
    }

    public override void OnPlayerVoted(ulong steamId, VotingType votingType)
    {
        _ = controller.HandleVoteAsync(steamId, votingType, _stateCts?.Token ?? CancellationToken.None);
    }

    public override void OnVoteStartRequested()
    {
        ZeepkistNetwork.SendCustomChatMessage(true, 0, "Voting is already active!", controller.ServermessageTitle);
    }

    public override void OnVoteStopRequested()
    {
        _ = controller.TransitionToStateAsync(new DisabledState(controller));
    }

    public override void OnVoteRestartRequested()
    {
        ZeepkistNetwork.SendCustomChatMessage(true, 0, "Restarting playlist voting...", controller.ServermessageTitle);
        _ = controller.TransitionToStateAsync(new ActiveState(controller));
    }

    public override void OnLobbyStateChanged(ZeepkistLobbyState lobbyState)
    {
        if (lobbyState != ZeepkistLobbyState.Racing)
        {
            _ = controller.TransitionToStateWithResultsAsync();
        }
    }
}
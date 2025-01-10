using ZeepkistClient;
using ZeepSDK.Messaging;

namespace PlaylistVoting;

public class StateInactive : State
{
    public StateInactive(Plugin plugin) : base(plugin)
    {
    }

    public override void Enter()
    {
        VoteStop.OnHandle += OnVoteStopOnOnHandle;
        VoteStart.OnHandle += OnVoteStartOnOnHandle;
    }

    private void OnVoteStartOnOnHandle()
    {
        if (!ZeepkistNetwork.LocalPlayer.isHost)
        {
            MessengerApi.LogWarning("You are not the host! Vote failed to start");
            return;
        }

        MessengerApi.LogSuccess("Vote successfully started!");
        Plugin.SwitchState(new StateActive(Plugin));
    }

    private void OnVoteStopOnOnHandle()
    {
        MessengerApi.LogWarning("Vote is not running");
    }

    public override void Exit()
    {
        VoteStop.OnHandle -= OnVoteStopOnOnHandle;
        VoteStart.OnHandle -= OnVoteStartOnOnHandle;
    }
}
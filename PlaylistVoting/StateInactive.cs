using ZeepSDK.Chat;
using ZeepSDK.Messaging;
using ZeepSDK.Racing;

namespace PlaylistVoting;

public class StateInactive : State
{
    public override void Enter()
    {
        VoteStop.OnHandle += OnVoteStopOnOnHandle;
        VoteStart.OnHandle += OnVoteStartOnOnHandle;
        ChatApi.SendMessage("/servermessage remove");
    }

    private void OnVoteStartOnOnHandle()
    {
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

    public StateInactive(Plugin plugin) : base(plugin)
    {
    }
}
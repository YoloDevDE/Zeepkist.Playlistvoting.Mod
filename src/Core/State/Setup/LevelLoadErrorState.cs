using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.State.Abstractions;
using PlaylistVoting.Infrastructure.UI;
using PlaylistVoting.Infrastructure.Zeepkist;
using ZeepUtils.Text;
using ZeepUtils.Zeepkist;

namespace PlaylistVoting.Core.State.Setup;

public class LevelLoadErrorState : VotingStateBase
{
    public LevelLoadErrorState(VotingController controller) : base(controller) { }

    public override void OnEnter()
    {
        Logger.Warn("Entered LevelLoadErrorState.");
        VotingDisplayManager.ClearDisplay();

        string msg = new RichText().Append("ERROR: Level failed to load!", b => b.Bold().Color("#FF0000")).Break().Append("The voting process is paused.", b => b.Color("#dddddd")).Break().Append("Waiting for next level or manual restart.", b => b.Size(80))
                                   .Build();

        ZeepkistNetworkHelper.SendLocalPrivateMessage(msg, ZeepkistNetworkHelper.CategoryError);

        ToastNotification.Error("Level load failed!");
    }

    public override void OnLevelLoaded()
    {
        Logger.Info("Level loaded, attempting to recover from error state.");
        Controller.TransitionTo(new InitState(Controller));
    }

    public override void OnVoteRestartRequested()
    {
        Controller.TransitionTo(new InitState(Controller));
    }
}
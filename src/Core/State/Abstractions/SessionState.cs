using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;

namespace PlaylistVoting.Core.State.Abstractions;

public abstract class SessionState : VotingStateBase
{
    protected readonly PlaylistSessionInfo Session;

    protected SessionState(VotingController controller, PlaylistSessionInfo session) : base(controller)
    {
        Session = session;
    }
}
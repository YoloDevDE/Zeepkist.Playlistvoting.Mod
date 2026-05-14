using PlaylistVoting.Data.Models;

namespace PlaylistVoting.Management.States.Abstractions;

public abstract class SessionState : VotingStateBase
{
    protected readonly PlaylistSessionInfo Session;

    protected SessionState(VotingController controller, PlaylistSessionInfo session) : base(controller)
    {
        Session = session;
    }
}
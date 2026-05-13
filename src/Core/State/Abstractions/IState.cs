namespace PlaylistVoting.Core.State.Abstractions;

public interface IState
{
    void OnEnter();
    void OnExit();
    void OnUpdate();
}
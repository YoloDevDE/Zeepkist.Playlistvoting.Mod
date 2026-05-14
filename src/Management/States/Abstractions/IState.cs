namespace PlaylistVoting.Management.States.Abstractions;

public interface IState
{
    void OnEnter();
    void OnExit();
    void OnUpdate();
}
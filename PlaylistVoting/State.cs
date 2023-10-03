namespace PlaylistVoting;

public abstract class State
{
    public Plugin Plugin;

    protected State(Plugin plugin)
    {
        Plugin = plugin;
    }

    public abstract void Enter();
    public abstract void Exit();
}
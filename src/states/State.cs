using PlaylistVoting.core;

namespace PlaylistVoting.states;

/// <summary>
///     Base class for all voting states.
/// </summary>
public abstract class State
{
    protected State(VotingManager manager)
    {
        Manager = manager;
    }

    protected VotingManager Manager { get; }

    /// <summary>Called when this state becomes active.</summary>
    public virtual void Enter()
    {
        VotingEventBus.Hub.GamePhaseChanged += OnGamePhaseChanged;
    }

    /// <summary>Called when this state is being left.</summary>
    public virtual void Exit()
    {
        VotingEventBus.Hub.GamePhaseChanged -= OnGamePhaseChanged;
    }

    protected virtual void OnGamePhaseChanged(GamePhase phase) { }
}
using UnityEngine;

namespace PlaylistVoting.states;

public abstract class State
{
    public Plugin Plugin;

    protected State(Plugin plugin)
    {
        Initialize(plugin);
    }

    public void Initialize(Plugin plugin)
    {
        Plugin = plugin;

        // Ensure the plugin has a valid GameObject
        if (plugin != null && plugin.gameObject != null)
        {
            plugin.gameObject.AddComponent(GetType());
        }
        else
        {
            Debug.LogWarning("Plugin or its GameObject is null. Initialization may fail.");
        }
    }

    public abstract void Enter();
    public abstract void Exit();
}
using BepInEx;
using HarmonyLib;
using PlaylistVoting.core;

namespace PlaylistVoting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;
    private VotingManager _votingManager;

    private void Awake()
    {
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();

        _votingManager = new VotingManager(Config, Logger);
        _ = _votingManager.InitializeAsync();

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded.");
    }
    

    private void OnDestroy()
    {
        _votingManager?.Dispose();
        _harmony?.UnpatchSelf();
    }
}
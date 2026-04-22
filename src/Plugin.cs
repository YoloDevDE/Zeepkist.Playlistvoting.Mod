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
    public static Plugin Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();

        VotingConfig.Init(Config);


        _votingManager = gameObject.AddComponent<VotingManager>();
        _votingManager.Initialize(Logger);
        DontDestroyOnLoad(gameObject);

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
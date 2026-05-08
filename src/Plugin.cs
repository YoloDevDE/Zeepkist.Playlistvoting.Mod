using BepInEx;
using HarmonyLib;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using YoloDev.Zeepkist;

namespace PlaylistVoting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;
    private VotingController _votingManager;
    public static Plugin Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
        ToastNotification.Initialize(Logger, MyPluginInfo.PLUGIN_NAME);

        VotingConfig.Init(Config);


        _votingManager = gameObject.AddComponent<VotingController>();
        _votingManager.Initialize(Logger);
        DontDestroyOnLoad(gameObject);

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
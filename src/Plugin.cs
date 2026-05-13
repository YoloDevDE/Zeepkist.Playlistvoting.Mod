using BepInEx;
using HarmonyLib;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Controllers;
using UnityEngine.SceneManagement;

namespace PlaylistVoting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;

    private bool _initialized;
    private string _previousScene = "";
    private VotingController _votingManager;
    public static Plugin Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        PlaylistVoting.Logger.Init(Logger);
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();

        VotingConfig.Init(Config);


        _votingManager = gameObject.AddComponent<VotingController>();

        SceneManager.sceneLoaded += OnSceneLoaded;

        DontDestroyOnLoad(gameObject);

        PlaylistVoting.Logger.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded.");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _harmony?.UnpatchSelf();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_initialized && scene.name == "3D_MainMenu" && _previousScene.StartsWith("Intro"))
        {
            _initialized = true;
            _votingManager.Initialize();
        }

        _previousScene = scene.name;
    }
}
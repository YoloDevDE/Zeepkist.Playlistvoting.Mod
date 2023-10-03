using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine.SceneManagement;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;
using System.Timers;

namespace PlaylistVoting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    public State state;

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
        ChatCommandApi.RegisterRemoteChatCommand<VoteYes>();
        ChatCommandApi.RegisterRemoteChatCommand<VoteNo>();
        ChatCommandApi.RegisterLocalChatCommand<VoteReset>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStart>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStop>();

        VoteReset.OnHandle += HandleRequestAsyncReset;

        this.state = new StateInactive(this);
        this.state.Enter();

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    public static async void HandleRequestAsyncReset()
    {
        var url = "https://yololurk.herokuapp.com/api/ronan/reset?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991";
        try
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    ChatApi.SendMessage(content);
                }
                else
                {
                    ChatApi.SendMessage($"Error: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            // Handle exception
            ChatApi.SendMessage($"Error: {ex.Message}");
        }
    }

    public void SwitchState(State state)
    {
        this.state.Exit();
        this.state = state;
        this.state.Enter();
    }
}

public class VoteYes : IRemoteChatCommand
{
    public string Prefix => "!";
    public string Command => "y";

    public string Description =>
        "Votes Yes!";

    public void Handle(ulong playerId, string arguments)
    {
        OnHandle?.Invoke(playerId);
    }


    // Event-Definition
    public static event Action<ulong> OnHandle;
}

public class VoteNo : IRemoteChatCommand
{
    public string Prefix => "!";
    public string Command => "n";

    public string Description =>
        "Votes No!";

    public void Handle(ulong playerId, string arguments)
    {
        OnHandle?.Invoke(playerId);
    }

    // Event-Definition
    public static event Action<ulong> OnHandle;
}

public class VoteReset : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "reset";

    public string Description =>
        "Reset the votes and prints the result";

    public void Handle(string arguments)
    {
        OnHandle?.Invoke();
    }

    // Event-Definition
    public static event Action OnHandle;
}

public class VoteStart : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote start";

    public string Description =>
        "Starts the Count!!";

    public void Handle(string arguments)
    {
        OnHandle?.Invoke();
    }

    // Event-Definition
    public static event Action OnHandle;
}

public class VoteStop : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "vote stop";

    public string Description =>
        "Stops the Count!!";

    public void Handle(string arguments)
    {
        OnHandle?.Invoke();
    }

    // Event-Definition
    public static event Action OnHandle;
}
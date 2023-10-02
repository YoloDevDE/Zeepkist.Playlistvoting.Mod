using System;
using System.Net.Http;
using BepInEx;
using HarmonyLib;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace FSMTest;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private static readonly HttpClient client = new();
    private Harmony harmony;

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
        ChatCommandApi.RegisterRemoteChatCommand<VoteYes>();
        ChatCommandApi.RegisterRemoteChatCommand<VoteNo>();
        ChatCommandApi.RegisterLocalChatCommand<VoteReset>();
        VoteYes.OnHandle += HandleRequestAsyncYes;
        VoteNo.OnHandle += HandleRequestAsyncNo;
        VoteReset.OnHandle += HandleRequestAsyncReset;

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }

    public static async void HandleRequestAsyncReset()
    {
        var url =
            "https://yololurk.herokuapp.com/api/ronan/reset?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991&twitchUser=R0nanC";
        HandleRequestAsync(url);
    }

    public static async void HandleRequestAsync(string url)
    {
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

    public static async void HandleRequestAsyncNo(ulong playerId)
    {
        var url =
            $"https://yololurk.herokuapp.com/api/ronan/no?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991&twitchUser={playerId}";

        HandleRequestAsync(url);
    }

    public static async void HandleRequestAsyncYes(ulong playerId)
    {
        var url =
            $"https://yololurk.herokuapp.com/api/ronan/yes?token=7DCD7DB2-03D9-427A-936C-5CDBD0610991&twitchUser={playerId}";

        HandleRequestAsync(url);
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
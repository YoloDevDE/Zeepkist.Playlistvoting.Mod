using System;
using System.Net.Http;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;
    private State _state;
    public static Plugin Instance { get; private set; }

    private ConfigEntry<string> messageFormat;
    private ConfigEntry<string> winColor;
    private ConfigEntry<string> tieColor;
    private ConfigEntry<string> loseColor;
    private ConfigEntry<string> winEmote;
    private ConfigEntry<string> tieEmote;
    private ConfigEntry<string> loseEmote;

    public string MessageFormat => messageFormat.Value;
    public string WinColor => winColor.Value;
    public string TieColor => tieColor.Value;
    public string LoseColor => loseColor.Value;
    public string WinEmote => winEmote.Value;
    public string TieEmote => tieEmote.Value;
    public string LoseEmote => loseEmote.Value;

    private void Awake()
        // [Info   : Unity Log] GetChatMessage: : <i>Command failed. Invalid Color. Accepted colors: red, orange, yellow, blue, green, pink, purple, black, white</i>
    {
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
        

        Instance = this;
        messageFormat = Config.Bind(
            "Settings",
            "MessageFormat",
            "Current Total -> %y/%n (y/n) %e",
            "Format of the voting message.");

        // Updated color options
        var colors = new AcceptableValueList<string>("Red", "Orange", "Yellow", "Blue", "Green", "Pink", "Purple",
            "Black", "White");

        winColor = Config.Bind<string>(
            "Colors",
            "Win",
            colors.AcceptableValues[4],
            new ConfigDescription("Color when yes votes are greater than no votes.", colors)
        );

        tieColor = Config.Bind<string>(
            "Colors",
            "Tie",
            colors.AcceptableValues[2],
            new ConfigDescription("Color when yes votes are equal to no votes.", colors)
        );

        loseColor = Config.Bind<string>(
            "Colors",
            "Lose",
            colors.AcceptableValues[0],
            new ConfigDescription("Color when no votes are greater than yes votes.", colors)
        );

        // Updated emote options
        var emotes = new AcceptableValueList<string>(ZeepkistEmojis.GetEmojis().ToArray());

        winEmote = Config.Bind<string>(
            "Emotes (%e)",
            "Win",
            ":yannicsmile:",
            new ConfigDescription("Emote when yes votes are greater than no votes.", emotes)
        );

        tieEmote = Config.Bind<string>(
            "Emotes (%e)",
            "Tie",
            ":yannics:",
            new ConfigDescription("Emote when yes votes are equal to no votes.", emotes)
        );
        loseEmote = Config.Bind<string>(
            "Emotes (%e)", 
            "Lose", 
            ":yannicmegas:", 
            new ConfigDescription("Emote when no votes are greater than yes votes.", emotes)
        );


        ChatCommandApi.RegisterRemoteChatCommand<VoteYes>();
        ChatCommandApi.RegisterRemoteChatCommand<VoteNo>();
        ChatCommandApi.RegisterLocalChatCommand<VoteReset>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStart>();
        ChatCommandApi.RegisterLocalChatCommand<VoteStop>();
        VoteReset.OnHandle += HandleRequestAsyncReset;

        _state = new StateInactive(this);
        _state.Enter();

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
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
        _state.Exit();
        _state = state;
        _state.Enter();
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
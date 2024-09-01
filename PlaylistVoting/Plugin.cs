using System;
using System.Collections.Generic;
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
    public string level, author, uid;
    private Harmony _harmony;
    private State _state;
    private ConfigEntry<string> loseColor;
    private ConfigEntry<string> loseEmote;

    private ConfigEntry<string> messageFormat;
    private ConfigEntry<string> tieColor;
    private ConfigEntry<string> tieEmote;
    private ConfigEntry<string> webToken;
    private ConfigEntry<string> winColor;
    private ConfigEntry<string> winEmote;
    public static Plugin Instance { get; private set; }
    public string MessageFormat => messageFormat.Value;
    public string WinColor => winColor.Value;
    public string TieColor => tieColor.Value;
    public string LoseColor => loseColor.Value;
    public string WinEmote => winEmote.Value;
    public string TieEmote => tieEmote.Value;
    public string LoseEmote => loseEmote.Value;
    public string WebToken => webToken.Value;

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
            "Customize the format of the voting results message with specific placeholders:\n\n%y: Number of yes votes\n%n: Number of no votes\n%e: Display emote\n%l: Level name\n%a: Author's name");

        // Updated color options
        AcceptableValueList<string> colors = new AcceptableValueList<string>("Red", "Orange", "Yellow", "Blue", "Green", "Pink", "Purple",
            "Black", "White");

        winColor = Config.Bind(
            "Colors",
            "Win",
            colors.AcceptableValues[4],
            new ConfigDescription("Color when yes votes are greater than no votes.", colors)
        );
        // Hinzufügen des webToken
        webToken = Config.Bind(
            "Web API", // Kategorie
            "WebToken", // Name der Einstellung
            "7DCD7DB2-03D9-427A-936C-5CDBD0610991", // Standardwert
            "Trust Yolo here"); // Beschreibung
        tieColor = Config.Bind(
            "Colors",
            "Tie",
            colors.AcceptableValues[2],
            new ConfigDescription("Color when yes votes are equal to no votes.", colors)
        );

        loseColor = Config.Bind(
            "Colors",
            "Lose",
            colors.AcceptableValues[0],
            new ConfigDescription("Color when no votes are greater than yes votes.", colors)
        );

        // Updated emote options
        AcceptableValueList<string> emotes = new AcceptableValueList<string>(ZeepkistEmojis.GetEmojis().ToArray());

        winEmote = Config.Bind(
            "Emotes (%e)",
            "Win",
            ":yannicsmile:",
            new ConfigDescription("Emote when yes votes are greater than no votes.", emotes)
        );

        tieEmote = Config.Bind(
            "Emotes (%e)",
            "Tie",
            ":yannics:",
            new ConfigDescription("Emote when yes votes are equal to no votes.", emotes)
        );
        loseEmote = Config.Bind(
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

        using (HttpClient httpClient = new HttpClient())
        {
            string levelUrl =
                $"https://yololurk.herokuapp.com/api/ronan/get/map/name?token={WebToken}";
            string authorUrl =
                $"https://yololurk.herokuapp.com/api/ronan/get/map/author?token={WebToken}";
            level = httpClient.GetAsync(levelUrl).Result.Content.ReadAsStringAsync().Result;
            author = httpClient.GetAsync(authorUrl).Result.Content.ReadAsStringAsync().Result;
        }

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

    public async void HandleRequestAsyncReset()
    {
        string resetUrl = $"https://yololurk.herokuapp.com/api/ronan/reset?token={WebToken}";
        try
        {
            using (HttpClient httpClient = new HttpClient())
            {
                // First, perform the reset
                HttpResponseMessage resetResponse = await httpClient.GetAsync(resetUrl);
                if (!resetResponse.IsSuccessStatusCode)
                {
                    ChatApi.SendMessage($"Error during reset: {resetResponse.StatusCode}");
                    return;
                }

                string resetResponseContent = await resetResponse.Content.ReadAsStringAsync();
                ChatApi.SendMessage(resetResponseContent);

                // Then, set the map and author
                level = PlayerManager.Instance.currentMaster.GlobalLevel.Name;
                author = PlayerManager.Instance.currentMaster.GlobalLevel.Author;
                uid = ZeepkistNetwork.CurrentLobby.LevelUID;

                string setMapUrl = "https://yololurk.herokuapp.com/api/ronan/set/map";
                FormUrlEncodedContent content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "token", $"{WebToken}" },
                    { "uid", uid },
                    { "map", level },
                    { "author", author }
                });

                HttpResponseMessage setMapResponse = await httpClient.PostAsync(setMapUrl, content);
                if (!setMapResponse.IsSuccessStatusCode)
                {
                    ChatApi.SendMessage($"Error setting map: {setMapResponse.StatusCode}");
                    return;
                }

                string setMapResponseContent = await setMapResponse.Content.ReadAsStringAsync();
                Logger.LogInfo(setMapResponseContent);
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
    public string Command => "vote reset";

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
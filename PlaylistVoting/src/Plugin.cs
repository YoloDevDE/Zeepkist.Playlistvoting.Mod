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
    private ConfigEntry<bool> deleteNoLevels;
    private ConfigEntry<bool> isTieLose;
    private ConfigEntry<string> loseEmote;

    private ConfigEntry<string> servermessageTitle;
    private ConfigEntry<string> tieEmote;
    private ConfigEntry<string> webToken;
    private ConfigEntry<string> winEmote;
    public static Plugin Instance { get; private set; }
    public bool DeleteNoLevels => deleteNoLevels.Value;
    public bool IsTieLose => isTieLose.Value;
    public string ServermessageTitle => servermessageTitle.Value;
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


        deleteNoLevels = Config.Bind(
            "Settings",
            "Delete rejected levels?",
            false,
            "Should a map that lost the vote be deleted from the playlist?");
        // isTieLose = Config.Bind(
        //     "Settings",
        //     "Tie = lose vote? (WIP -> Does not do anything rn)",
        //     true,
        //     "Should a tied vote equals lose? (true = lose, false = win)");
        servermessageTitle = Config.Bind(
            "Settings",
            "Servermessage",
            "Playlist-Voting",
            "Customize the title of the servermessage");


        // Hinzufügen des webToken
        webToken = Config.Bind(
            "Web API", // Kategorie
            "WebToken", // Name der Einstellung
            "XXXXX-XXXXX-XXX-XXX-XXX", // Standardwert
            "Trust Yolo here"); // Beschreibung


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


        ChatCommandApi.RegisterMixedChatCommand<VoteYes>();
        ChatCommandApi.RegisterMixedChatCommand<VoteNo>();
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

    public async void HandleRequestAsyncReset(bool printResults)
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
                if (printResults)
                {
                    ChatApi.SendMessage(resetResponseContent);
                    ZeepkistNetwork.SendCustomChatMessage(true, 0,
                        $"<color=#f0f0f0>{resetResponseContent}<br>----------------</color>", Instance.ServermessageTitle);
                }

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

    public async void HandleRequestAsyncReset()
    {
        HandleRequestAsyncReset(true);
    }

    public void SwitchState(State state)
    {
        _state.Exit();
        _state = state;
        _state.Enter();
    }
}
using BepInEx.Logging;
using PlaylistVoting.Core.Controllers;
using UnityEngine;
using ZeepSDK.Messaging;

namespace PlaylistVoting.Infrastructure.UI;

public static class ToastNotification
{
    private static readonly ITaggedMessenger Messenger = MessengerApi.CreateTaggedMessenger(MyPluginInfo.PLUGIN_NAME);

    public static string Tag => Messenger.Tag;

    private static void Log(LogLevel level, string message)
    {
        VotingController.Instance?.Logger?.Log(level, message);
    }

    public static void Info(string message, float duration = 2.5f)
    {
        Log(LogLevel.Info, message);
        Messenger.Log(message, duration);
    }

    public static void Success(string message, float duration = 2.5f)
    {
        Log(LogLevel.Info, message);
        Messenger.LogSuccess(message, duration);
    }

    public static void Warning(string message, float duration = 2.5f)
    {
        Log(LogLevel.Warning, message);
        Messenger.LogWarning(message, duration);
    }

    public static void Error(string message, float duration = 2.5f)
    {
        Log(LogLevel.Error, message);
        Messenger.LogError(message, duration);
    }


    public static void Custom(string message,
        Color backgroundColor = default,
        Color textColor = default,
        float duration = 2.5f)
    {
        if (backgroundColor == default)
        {
            backgroundColor = Color.black;
        }

        if (textColor == default)
        {
            textColor = Color.white;
        }

        Log(LogLevel.Info, message);
        Messenger.LogCustomColors(message, textColor, backgroundColor, duration);
    }
}
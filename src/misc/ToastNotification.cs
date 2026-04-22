using UnityEngine;
using ZeepSDK.Messaging;

namespace PlaylistVoting.misc;

public static class ToastNotification
{
    private static readonly ITaggedMessenger Messenger = MessengerApi.CreateTaggedMessenger(MyPluginInfo.PLUGIN_NAME);

    public static string Tag => Messenger.Tag;

    public static void Info(string message, float duration = 2.5f)
    {
        Messenger.Log(message, duration);
    }

    public static void Success(string message, float duration = 2.5f)
    {
        Messenger.LogSuccess(message, duration);
    }

    public static void Warning(string message, float duration = 2.5f)
    {
        Messenger.LogWarning(message, duration);
    }

    public static void Error(string message, float duration = 2.5f)
    {
        Messenger.LogError(message, duration);
    }

    public static void Custom(string message, float duration = 2.5f)
    {
        Custom(message, Color.black, duration);
    }

    public static void Custom(string message, Color backgroundColor, float duration = 2.5f)
    {
        Custom(message, backgroundColor, Color.white, duration);
    }

    public static void Custom(string message, Color backgroundColor, Color textColor, float duration = 2.5f)
    {
        Messenger.LogCustomColors(message, textColor, backgroundColor, duration);
    }
}
using BepInEx.Logging;

namespace PlaylistVoting;

public static class Logger
{
    private static ManualLogSource _logger;

    public static void Init(ManualLogSource logger)
    {
        _logger = logger;
    }

    public static void Info(object data) => _logger?.LogInfo(data);
    public static void Warn(object data) => _logger?.LogWarning(data);
    public static void Error(object data) => _logger?.LogError(data);
    public static void Debug(object data) => _logger?.LogDebug(data);
    public static void Fatal(object data) => _logger?.LogFatal(data);
    public static void Message(object data) => _logger?.LogMessage(data);
}
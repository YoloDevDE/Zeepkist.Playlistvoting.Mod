using HarmonyLib;
using UnityEngine;

namespace PlaylistVoting;

[HarmonyPatch(typeof(Debug), "Log", typeof(object))]
public class SuppressDebugLogPatch
{
    public static bool Prefix(object message) =>
        // Check if this is the spammy log we want to suppress
        message == null
        || !message.ToString().Contains("GetSlipAndSurfaceList().count:");
    // Don't log it
    // Allow all other Debug.Log calls
}
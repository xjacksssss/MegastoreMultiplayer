using HarmonyLib;
using MegastoreMultiplayer.Network;

// Only the host drives day progression — clients follow.
// Both methods are public on TimeManager so Harmony resolves them by name.

[HarmonyPatch(typeof(TimeManager), nameof(TimeManager.OnEndDay))]
public static class TimeManager_OnEndDay_Patch
{
    static void Postfix()
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteDayEnded();
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(TimeManager), nameof(TimeManager.StartTheNewDay))]
public static class TimeManager_StartTheNewDay_Patch
{
    static void Postfix()
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteNewDayStarted();
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

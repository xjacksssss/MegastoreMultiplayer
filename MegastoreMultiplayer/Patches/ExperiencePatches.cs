using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(ExperienceManager), nameof(ExperienceManager.LevelUp))]
public static class ExperienceManager_LevelUp_Patch
{
    static void Postfix(ExperienceManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteLevelUp(__instance.CurrentLevel);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

// Broadcast XP progress after every sale so the remote XP bar stays in sync.
// OnPaymentDone is private — patched by name.
[HarmonyPatch(typeof(ExperienceManager), "OnPaymentDone")]
public static class ExperienceManager_OnPaymentDone_Patch
{
    static void Postfix(ExperienceManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteXpUpdate(__instance.CurrentXP, __instance.CurrentLevel);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

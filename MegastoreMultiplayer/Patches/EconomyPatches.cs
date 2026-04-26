using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(EconomyManager), nameof(EconomyManager.AddSoftCurrency))]
public static class EconomyManager_AddSoftCurrency_Patch
{
    static void Postfix(EconomyManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteMoneyUpdate(__instance.SoftCurrency);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(EconomyManager), nameof(EconomyManager.RemoveSoftCurrency))]
public static class EconomyManager_RemoveSoftCurrency_Patch
{
    static void Postfix(EconomyManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteMoneyUpdate(__instance.SoftCurrency);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

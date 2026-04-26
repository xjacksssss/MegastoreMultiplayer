using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(GrowthManager), nameof(GrowthManager.PurchaseGrowth))]
public static class GrowthManager_PurchaseGrowth_Patch
{
    static void Postfix()
    {
        GrowthTracker.Record();
        if (!MultiplayerManager.IsRunning) return;
        if (NetApply.IsApplying) return;
        var w = NetMessages.WriteStoreGrowth();
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

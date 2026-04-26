using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(PriceManager), nameof(PriceManager.SetUnitPrice))]
public static class PriceManager_SetUnitPrice_Patch
{
    static void Postfix(ProductType productType, float newPrice)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WritePriceUpdate((int)productType, newPrice);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

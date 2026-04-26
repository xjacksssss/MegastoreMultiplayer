using HarmonyLib;
using MegastoreMultiplayer.Network;

// Vending machine sync coverage:
//   storedMoney (cash accumulation) → ProductPurchased / CollectMoney patches below.
//   Stock depletion (product count)  → Shelf.RemoveProduct → ShelfPatches → ShelfUpdate packet.
//                                      No separate vending-stock message is needed.

[HarmonyPatch(typeof(VendingPlaceable), nameof(VendingPlaceable.ProductPurchased))]
public static class VendingPlaceable_ProductPurchased_Patch
{
    static void Postfix(VendingPlaceable __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying || !MultiplayerManager.IsHost) return;
        float money = HarmonyLib.Traverse.Create(__instance).Field("storedMoney").GetValue<float>();
        var w = NetMessages.WriteVendingMoneyUpdate((int)__instance.Type, __instance.PlaceableID, money);
        MultiplayerManager.SendToAllReliable(w);
    }
}

[HarmonyPatch(typeof(VendingPlaceable), nameof(VendingPlaceable.CollectMoney))]
public static class VendingPlaceable_CollectMoney_Patch
{
    static void Postfix(VendingPlaceable __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var w = NetMessages.WriteVendingMoneyUpdate((int)__instance.Type, __instance.PlaceableID, 0f);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

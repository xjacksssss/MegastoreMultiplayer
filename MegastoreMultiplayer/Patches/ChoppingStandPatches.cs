using System.Collections.Generic;
using HarmonyLib;
using MegastoreMultiplayer.Network;

// Chopping stand (seafood counter) sync.
//
// Host-authoritative model:
//   - The NPC customer AI always runs on the host.  AskForProduct fires on the host
//     and is broadcast to clients so they see products appear on the stand.
//   - If a CLIENT player is at the stand and finishes chopping, they send
//     ChoppingProductGiven to the host.  The host then calls GivePreparedProduct
//     using its own awaitingCustomer reference to complete the NPC transaction.
//   - If the HOST player is at the stand, GivePreparedProduct fires locally and
//     is broadcast to clients to clear the stand.
//
// Limitation: chop-by-chop knife animation is not synced (visual only on each machine).

[HarmonyPatch(typeof(ChoppingStandPlaceable), nameof(ChoppingStandPlaceable.OnStandClicked))]
public static class ChoppingStandPlaceable_OnStandClicked_Patch
{
    static void Postfix(ChoppingStandPlaceable __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var w = NetMessages.WriteChoppingStandSit((int)__instance.Type, __instance.PlaceableID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// OverChoppingStand is private — patch by string name.
[HarmonyPatch(typeof(ChoppingStandPlaceable), "OverChoppingStand")]
public static class ChoppingStandPlaceable_OverChoppingStand_Patch
{
    static void Postfix(ChoppingStandPlaceable __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var w = NetMessages.WriteChoppingStandLeave((int)__instance.Type, __instance.PlaceableID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// AskForProduct is called by the NPC customer AI — only runs on the host.
[HarmonyPatch(typeof(ChoppingStandPlaceable), nameof(ChoppingStandPlaceable.AskForProduct))]
public static class ChoppingStandPlaceable_AskForProduct_Patch
{
    static void Postfix(ChoppingStandPlaceable __instance, List<Product> products, Customer customer)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying || !MultiplayerManager.IsHost) return;
        if (products == null || products.Count == 0 || customer == null) return;

        int netId       = NpcNetworkManager.GetNetworkId(customer);
        int productType = (int)products[0].Data.type;
        int count       = products.Count;
        float price     = SingletonBehaviour<PriceManager>.Instance.GetUnitPrice(products[0].Data.type) * count;

        var w = NetMessages.WriteChoppingAskProduct(
            (int)__instance.Type, __instance.PlaceableID,
            productType, count, price, netId);
        MultiplayerManager.SendToAllReliable(w);
    }
}

// GivePreparedProduct fires when the product is handed to the NPC.
[HarmonyPatch(typeof(ChoppingStandPlaceable), nameof(ChoppingStandPlaceable.GivePreparedProduct))]
public static class ChoppingStandPlaceable_GivePreparedProduct_Patch
{
    static void Postfix(ChoppingStandPlaceable __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var w = NetMessages.WriteChoppingProductGiven((int)__instance.Type, __instance.PlaceableID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

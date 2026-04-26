using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(Shelf), nameof(Shelf.Initialize))]
public static class Shelf_Initialize_Patch
{
    static void Postfix(Shelf __instance)
    {
        ShelfRegistry.Register(__instance);
    }
}

[HarmonyPatch(typeof(Shelf), nameof(Shelf.PlaceProduct), new[] { typeof(Product) })]
public static class Shelf_PlaceProduct_Patch
{
    static void Postfix(Shelf __instance, Product product)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteShelfUpdate(
            ShelfRegistry.GetId(__instance),
            (int)product.Data.type,
            __instance.GetProductCount());
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(Shelf), nameof(Shelf.RemoveProduct))]
public static class Shelf_RemoveProduct_Patch
{
    static void Postfix(Shelf __instance, Product product)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteShelfUpdate(
            ShelfRegistry.GetId(__instance),
            (int)product.Data.type,
            __instance.GetProductCount());
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

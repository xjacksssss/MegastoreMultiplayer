using HarmonyLib;
using MegastoreMultiplayer.Network;

// Syncs tray placement into an oven (dough going in before cooking starts).
// The cooking itself is already synced in OvenPatches (TurnOn / OnCookingFinished).
// Removing cooked goods is handled by TrayPatches (TrayManager.PickUpTray).
//
// Oven.PutTrayBack is called when a player slots a dough tray into an oven slot.
// PutTrayBackAnimated is the animated variant — both need to be patched.

[HarmonyPatch(typeof(Oven), nameof(Oven.PutTrayBack))]
public static class Oven_PutTrayBack_Patch
{
    static void Postfix(Oven __instance, Tray tray)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteBakeryDoughLoaded(__instance.FurnitureID, tray.TrayID);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(Oven), nameof(Oven.PutTrayBackAnimated))]
public static class Oven_PutTrayBackAnimated_Patch
{
    static void Postfix(Oven __instance, Tray tray)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteBakeryDoughLoaded(__instance.FurnitureID, tray.TrayID);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

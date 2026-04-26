using HarmonyLib;
using MegastoreMultiplayer.Network;

// Register every Tray when it initialises so TrayRegistry can look it up by ID.

[HarmonyPatch(typeof(Tray), "Initialize")]
public static class Tray_Initialize_Patch
{
    static void Postfix(Tray __instance, int trayID)
    {
        TrayRegistry.Register(trayID, __instance);
    }
}

// Sync tray pick-up: hide the tray on all other clients.

[HarmonyPatch(typeof(TrayManager), nameof(TrayManager.PickUpTray))]
public static class TrayManager_PickUpTray_Patch
{
    static void Postfix(Tray tray)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        if (MultiplayerManager.IsHost) NetMessages.LockTray(tray.TrayID);
        var w = NetMessages.WriteTrayPickedUp(tray.TrayID);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

// Sync tray put-down: PlaceTray() is private — Harmony patches by string name.
// At this point pickedTray is still held; its world position is the release point.

[HarmonyPatch(typeof(TrayManager), "PlaceTray")]
public static class TrayManager_PlaceTray_Patch
{
    static void Prefix(TrayManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var tray = __instance.PickedTray;
        if (tray == null) return;
        // Host unlocks here because ApplyTrayPutDown (the only other unlock site) only
        // runs on the receiver — the host never receives its own broadcasts.
        if (MultiplayerManager.IsHost) NetMessages.UnlockTray(tray.TrayID);
        var w = NetMessages.WriteTrayPutDown(
            tray.TrayID,
            tray.transform.position,
            tray.transform.eulerAngles);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

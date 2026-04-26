using MegastoreMultiplayer.Network;

// Sync the box "opened" visual when a player cuts/opens a box lid.
// This class is NOT decorated with [HarmonyPatch] because the exact method name
// must be confirmed with dnSpy. Plugin.Awake() probes candidate names and applies
// this postfix manually so a wrong guess doesn't crash PatchAll().
public static class BoxManager_Open_Patch
{
    // Called by Plugin.cs manual patch — __instance is BoxManager or Box depending on
    // which overload the game uses.
    public static void Postfix(object __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;

        Box box = __instance as Box;
        if (box == null)
        {
            // Might be BoxManager; try to get picked box from it.
            var bm = __instance as BoxManager;
            box = bm?.GetPickedBox();
        }
        if (box == null) return;

        var w = NetMessages.WriteBoxOpened(box.BoxID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

using HarmonyLib;
using MegastoreMultiplayer.Network;

// Trash is the game's recycling/disposal object. Boxes thrown in with sell=false
// are deleted; sell=true sells them for money (handled by EconomyManager patches).
// We sync the box deletion so clients remove the box from their scene.

[HarmonyPatch(typeof(Trash), nameof(Trash.DeleteBoxTrash))]
public static class Trash_DeleteBoxTrash_Patch
{
    static void Prefix(Box box)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        if (box == null) return;
        var w = NetMessages.WriteRecyclingDone(box.BoxID);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

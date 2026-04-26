using HarmonyLib;
using MegastoreMultiplayer.Network;

// Syncs store name and sign style when a player renames the store.
// RenameAndClose() is the private method wired to the OK button in the
// SupermarketNameWindow UI — it saves the name and updates the 3D signs.

[HarmonyPatch(typeof(SupermarketNameWindow), "RenameAndClose")]
public static class SupermarketNameWindow_RenameAndClose_Patch
{
    static void Postfix(SupermarketNameWindow __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        string name = HarmonyLib.Traverse.Create(__instance).Field("currentName").GetValue<string>();
        if (string.IsNullOrEmpty(name)) return;
        var w = NetMessages.WriteStoreName(name);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

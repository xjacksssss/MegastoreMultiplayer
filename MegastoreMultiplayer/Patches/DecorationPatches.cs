using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(DecorationManager), nameof(DecorationManager.UseDecoration))]
public static class DecorationManager_UseDecoration_Patch
{
    static void Postfix(DecorationUI.DecorationType type, int id)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var w = NetMessages.WriteDecorationUsed((int)type, id);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

using HarmonyLib;
using MegastoreMultiplayer.Network;

// ── Store open / closed sign ──────────────────────────────────────────────────

[HarmonyPatch(typeof(OpenCloseLabel), nameof(OpenCloseLabel.OnLabelClicked))]
public static class OpenCloseLabel_OnLabelClicked_Patch
{
    static void Postfix(OpenCloseLabel __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteStoreOpenClose(__instance.IsOpen);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

// ── Indoor lights toggle ──────────────────────────────────────────────────────

[HarmonyPatch(typeof(LightSwitch), nameof(LightSwitch.OnMouseButtonDown))]
public static class LightSwitch_OnMouseButtonDown_Patch
{
    static void Postfix(LightSwitch __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteLightToggle(__instance.IsOn);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

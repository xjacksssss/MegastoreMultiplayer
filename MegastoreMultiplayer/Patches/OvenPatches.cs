using HarmonyLib;
using MegastoreMultiplayer.Network;

// Register every oven when it initialises so OvenRegistry can look it up by FurnitureID.

[HarmonyPatch(typeof(Oven), nameof(Oven.InitializeOldFurniture))]
public static class Oven_InitializeOld_Patch
{
    static void Postfix(Oven __instance)
    {
        OvenRegistry.Register(__instance);
    }
}

[HarmonyPatch(typeof(Oven), nameof(Oven.InitializeNewFurniture))]
public static class Oven_InitializeNew_Patch
{
    static void Postfix(Oven __instance)
    {
        OvenRegistry.Register(__instance);
    }
}

// Sync oven turn-on. Postfix fires after cooking == true is set.

[HarmonyPatch(typeof(Oven), nameof(Oven.TurnOn))]
public static class Oven_TurnOn_Patch
{
    static void Postfix(Oven __instance)
    {
        if (!__instance.Cooking) return; // TurnOn was a no-op (already cooking or reserved)
        OvenRegistry.RecordStart(__instance.FurnitureID); // always track, even if not in MP
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        // elapsed = 0 for a fresh start; always 0 here since RecordStart just ran.
        var w = NetMessages.WriteOvenStart(__instance.FurnitureID, 0f);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

// Sync cooking finished. OnCookingFinished is private — Harmony patches by string name.

[HarmonyPatch(typeof(Oven), "OnCookingFinished")]
public static class Oven_OnCookingFinished_Patch
{
    static void Postfix(Oven __instance)
    {
        OvenRegistry.ClearStart(__instance.FurnitureID);
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteOvenComplete(__instance.FurnitureID);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

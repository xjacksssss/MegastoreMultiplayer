using HarmonyLib;
using MegastoreMultiplayer.Network;
using UnityEngine;

// ── Placeable registration ────────────────────────────────────────────────────

[HarmonyPatch(typeof(Placeable), nameof(Placeable.InitializeOldPlaceable))]
public static class Placeable_InitializeOld_Patch
{
    static void Postfix(Placeable __instance) => PlaceableRegistry.Register(__instance);
}

[HarmonyPatch(typeof(Placeable), nameof(Placeable.InitializeNewPlaceable))]
public static class Placeable_InitializeNew_Patch
{
    static void Postfix(Placeable __instance) => PlaceableRegistry.Register(__instance);
}

// ── Furniture registration ────────────────────────────────────────────────────

[HarmonyPatch(typeof(Furniture), nameof(Furniture.InitializeOldFurniture))]
public static class Furniture_InitializeOld_Patch
{
    static void Postfix(Furniture __instance) => FurnitureRegistry.Register(__instance);
}

[HarmonyPatch(typeof(Furniture), nameof(Furniture.InitializeNewFurniture))]
public static class Furniture_InitializeNew_Patch
{
    static void Postfix(Furniture __instance) => FurnitureRegistry.Register(__instance);
}

// ── Sync on placement confirmed ───────────────────────────────────────────────

[HarmonyPatch(typeof(Placeable), nameof(Placeable.OnPlacementEnded))]
public static class Placeable_OnPlacementEnded_Patch
{
    static void Postfix(Placeable __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WritePlaceableMoved(
            (int)__instance.Type,
            __instance.PlaceableID,
            __instance.transform.position,
            __instance.transform.eulerAngles);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(Furniture), nameof(Furniture.OnPlacementEnded))]
public static class Furniture_OnPlacementEnded_Patch
{
    static void Postfix(Furniture __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteFurnitureMoved(
            (int)__instance.Type,
            __instance.FurnitureID,
            __instance.transform.position,
            __instance.transform.eulerAngles);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

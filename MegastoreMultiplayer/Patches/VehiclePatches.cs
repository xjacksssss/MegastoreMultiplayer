using HarmonyLib;
using MegastoreMultiplayer.Network;

// ── Return-to-upright sync ────────────────────────────────────────────────────
// SaveLocation fires after the leave-animation completes, capturing the final
// parked position/rotation. Broadcasting it reliably fixes the "trolley stays
// tilted on remote" problem caused by position updates stopping mid-tilt.

[HarmonyPatch(typeof(VehichleInteractable), nameof(VehichleInteractable.SaveLocation))]
public static class VehicleSaveLocation_Patch
{
    static void Postfix(VehichleInteractable __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteVehiclePosition(
            (int)__instance.VehicleType,
            __instance.VehicleID,
            __instance.transform.position,
            __instance.transform.eulerAngles);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// ── Box loaded onto vehicle by click ─────────────────────────────────────────

[HarmonyPatch(typeof(HandTruck), nameof(HandTruck.PlaceBoxByClick))]
public static class HandTruck_PlaceBoxByClick_Patch
{
    static void Postfix(HandTruck __instance, Box box, bool __result)
    {
        if (!__result) return; // game rejected the placement
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteBoxOnVehicle(box.BoxID, (int)__instance.VehicleType, __instance.VehicleID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// ── Box loaded onto vehicle by hand (private method — patched by name) ────────

[HarmonyPatch(typeof(HandTruck), "OnPlaceProductByHand")]
public static class HandTruck_OnPlaceProductByHand_Patch
{
    static void Postfix(HandTruck __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        // The box that was just loaded is the last entry in containedBoxIDs.
        var ids = HarmonyLib.Traverse.Create(__instance).Field("containedBoxIDs").GetValue<System.Collections.Generic.List<int>>();
        if (ids == null || ids.Count == 0) return;
        int boxId = ids[ids.Count - 1];
        var w = NetMessages.WriteBoxOnVehicle(boxId, (int)__instance.VehicleType, __instance.VehicleID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// ── All boxes unloaded from vehicle (private method — patched by name) ────────

[HarmonyPatch(typeof(HandTruck), "UnloadBoxes")]
public static class HandTruck_UnloadBoxes_Patch
{
    static void Prefix(HandTruck __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteVehicleUnloaded((int)__instance.VehicleType, __instance.VehicleID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

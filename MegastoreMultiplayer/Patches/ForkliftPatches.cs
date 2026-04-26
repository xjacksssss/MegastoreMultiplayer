using HarmonyLib;
using MegastoreMultiplayer.Network;
using UnityEngine;

// ── PalletCarrierVehicle (base for Forklift and PalletTruck) ─────────────────
// OnPalletTaken fires when the vehicle secures a pallet.
// ReleasePallet fires when the vehicle drops it.

[HarmonyPatch(typeof(PalletCarrierVehicle), "OnPalletTaken")]
public static class PalletCarrierVehicle_OnPalletTaken_Patch
{
    static void Postfix(PalletCarrierVehicle __instance, int palletID)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        if (MultiplayerManager.IsHost) NetMessages.LockPallet(palletID);
        var w = NetMessages.WritePalletPickedUp(
            (int)__instance.VehicleType, __instance.VehicleID, palletID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(PalletCarrierVehicle), "ReleasePallet")]
public static class PalletCarrierVehicle_ReleasePallet_Patch
{
    // Capture the pallet before the field is cleared by the original method.
    private static Pallet _pallet;

    static void Prefix(PalletCarrierVehicle __instance)
    {
        _pallet = HarmonyLib.Traverse.Create(__instance)
            .Field("containedPallet").GetValue<Pallet>();
    }

    static void Postfix()
    {
        if (_pallet == null) return;
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) { _pallet = null; return; }
        if (MultiplayerManager.IsHost) NetMessages.UnlockPallet(_pallet.PalletID);
        var w = NetMessages.WritePalletDropped(
            _pallet.PalletID,
            _pallet.transform.position,
            _pallet.transform.eulerAngles);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
        _pallet = null;
    }
}

// ── PalletManager (player picking up / putting down pallets on foot) ──────────

[HarmonyPatch(typeof(PalletManager), nameof(PalletManager.PickUpPallet))]
public static class PalletManager_PickUpPallet_Patch
{
    static void Postfix(Pallet pallet)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        if (MultiplayerManager.IsHost) NetMessages.LockPallet(pallet.PalletID);
        // vehicleType = -1 means "player on foot"
        var w = NetMessages.WritePalletPickedUp(-1, -1, pallet.PalletID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(PalletManager), nameof(PalletManager.PutPallet))]
public static class PalletManager_PutPallet_Patch
{
    // Capture pallet reference before PutPallet nulls the carried reference.
    private static Pallet _pallet;

    static void Prefix(PalletManager __instance)
    {
        _pallet = HarmonyLib.Traverse.Create(__instance)
            .Field("carriedPallet").GetValue<Pallet>();
    }

    static void Postfix()
    {
        if (_pallet == null) return;
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) { _pallet = null; return; }
        if (MultiplayerManager.IsHost) NetMessages.UnlockPallet(_pallet.PalletID);
        var w = NetMessages.WritePalletDropped(
            _pallet.PalletID,
            _pallet.transform.position,
            _pallet.transform.eulerAngles);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
        _pallet = null;
    }
}

[HarmonyPatch(typeof(PalletManager), nameof(PalletManager.ThrowPallet))]
public static class PalletManager_ThrowPallet_Patch
{
    private static Pallet _pallet;

    static void Prefix(PalletManager __instance)
    {
        _pallet = HarmonyLib.Traverse.Create(__instance)
            .Field("carriedPallet").GetValue<Pallet>();
    }

    static void Postfix()
    {
        if (_pallet == null) return;
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) { _pallet = null; return; }
        if (MultiplayerManager.IsHost) NetMessages.UnlockPallet(_pallet.PalletID);
        var rb = _pallet.GetComponent<Rigidbody>();
        var vel = rb != null ? rb.linearVelocity : Vector3.zero;
        var w = NetMessages.WritePalletDropped(
            _pallet.PalletID,
            _pallet.transform.position,
            _pallet.transform.eulerAngles,
            vel);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
        _pallet = null;
    }
}

[HarmonyPatch(typeof(PalletManager), nameof(PalletManager.TrashPallet))]
public static class PalletManager_TrashPallet_Patch
{
    private static Pallet _pallet;

    static void Prefix(PalletManager __instance)
    {
        _pallet = HarmonyLib.Traverse.Create(__instance)
            .Field("carriedPallet").GetValue<Pallet>();
    }

    static void Postfix()
    {
        if (_pallet == null) return;
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) { _pallet = null; return; }
        if (MultiplayerManager.IsHost) NetMessages.UnlockPallet(_pallet.PalletID);
        var w = NetMessages.WritePalletTrashed(_pallet.PalletID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
        _pallet = null;
    }
}

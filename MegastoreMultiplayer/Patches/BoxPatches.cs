using System.Collections;
using HarmonyLib;
using MegastoreMultiplayer;
using MegastoreMultiplayer.Network;
using UnityEngine;

// ── Pick up ───────────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(BoxManager), nameof(BoxManager.PickUpBox))]
public static class BoxManager_PickUpBox_Patch
{
    static void Postfix(Box box)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        if (MultiplayerManager.IsHost) NetMessages.LockBox(box.BoxID, -1); // -1 = host carrier
        ProductData product = box.GetContainedProduct();
        var w = NetMessages.WriteBoxPicked(box.BoxID, product != null ? (int)product.type : -1, box.ProductCount);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// ── Throw (physics-based) ─────────────────────────────────────────────────────
// Broadcasts immediately in Postfix so the remote sees the box flying straight
// away. The velocity is applied on the remote so it follows the same arc.

[HarmonyPatch(typeof(BoxManager), nameof(BoxManager.ThrowBox))]
public static class BoxManager_ThrowBox_Patch
{
    // Tells the PutBox coroutine that the player threw instead of placing.
    internal static bool WasThrown;

    static void Prefix()
    {
        WasThrown = true;
    }

    static void Postfix(BoxManager __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var box = BoxManager_PutBox_Patch.PendingBox;
        BoxManager_PutBox_Patch.PendingBox = null;
        if (box == null) return;

        if (MultiplayerManager.IsHost) NetMessages.UnlockBox(box.BoxID);
        var w = NetMessages.WriteBoxThrown(box.BoxID, box.transform.position, box.RigidBody.linearVelocity, peerId: -1);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// ── Put down (ghost-placement mode — G key) ───────────────────────────────────
// PutBox() enters a placement mode. The confirmation fires a private delegate
// that nulls pickedBox — OnBoxPut / OnBoxPutWithoutThrow are not called here.
// We save the box reference then poll until IsBoxPicked goes false.

[HarmonyPatch(typeof(BoxManager), nameof(BoxManager.PutBox))]
public static class BoxManager_PutBox_Patch
{
    internal static Box PendingBox;

    static void Prefix(BoxManager __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        PendingBox = __instance.GetPickedBox();
        BoxManager_ThrowBox_Patch.WasThrown = false;
        if (PendingBox != null)
            Plugin.Instance.StartCoroutine(WaitForRelease(__instance, PendingBox));
    }

    private static IEnumerator WaitForRelease(BoxManager bm, Box box)
    {
        // Give the player up to 60 s to confirm the placement.
        float deadline = Time.time + 60f;
        while (Time.time < deadline)
        {
            // Player threw the box instead — ThrowBox patch handles broadcast.
            if (BoxManager_ThrowBox_Patch.WasThrown) yield break;

            // Box released (placement confirmed or a different action nulled it).
            if (!bm.IsBoxPicked || bm.GetPickedBox() != box) break;

            yield return new WaitForSeconds(0.05f);
        }

        if (BoxManager_ThrowBox_Patch.WasThrown) yield break;
        if (box == null || !box.gameObject.activeSelf) yield break;

        // Brief wait for the box to settle after placement animation.
        yield return new WaitForSeconds(0.2f);

        var w = NetMessages.WriteBoxDropped(box.BoxID, box.transform.position, box.transform.eulerAngles);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);

        PendingBox = null;
    }
}

// ── OnBoxPut / OnBoxPutWithoutThrow ──────────────────────────────────────────
// These fire when a box is snapped onto a surface (not via the ghost-placement
// G-key flow). Prefix captures pickedBox before it is nulled.

[HarmonyPatch(typeof(BoxManager), nameof(BoxManager.OnBoxPut))]
public static class BoxManager_OnBoxPut_Patch
{
    static void Prefix(BoxManager __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var box = __instance.GetPickedBox();
        if (box == null) return;
        if (MultiplayerManager.IsHost) NetMessages.UnlockBox(box.BoxID);
        var w = NetMessages.WriteBoxDropped(box.BoxID, box.transform.position, box.transform.eulerAngles);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(BoxManager), nameof(BoxManager.OnBoxPutWithoutThrow))]
public static class BoxManager_OnBoxPutWithoutThrow_Patch
{
    static void Prefix(BoxManager __instance)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying) return;
        var vm = SingletonBehaviour<VehicleManager>.Instance;
        if (vm != null && vm.IsOnVehicle) return;
        var box = __instance.GetPickedBox();
        if (box == null) return;
        if (MultiplayerManager.IsHost) NetMessages.UnlockBox(box.BoxID);
        var w = NetMessages.WriteBoxDropped(box.BoxID, box.transform.position, box.transform.eulerAngles);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

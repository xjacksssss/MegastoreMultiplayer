using System.Collections.Generic;
using HarmonyLib;
using MegastoreMultiplayer.Network;
using UnityEngine;

// ── Automated cashier scan (NPC cashier) ────────────────────────────────────
// Unchanged from before — only fires when isAutomated is true.

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.CheckoutStep))]
public static class CheckoutManager_CheckoutStep_Patch
{
    static void Postfix(CheckoutManager __instance, float speedMultiplier)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        if (!MultiplayerManager.IsHost) return;
        var w = NetMessages.WriteCheckoutStep(__instance.FurnitureID, speedMultiplier);
        MultiplayerManager.SendToAllReliable(w);
    }
}

// ── Customer joins queue ─────────────────────────────────────────────────────
// Called by customer NPC AI on the host when the customer walks to the desk.
// Without this, the client's productsPlaced list is empty and scanning is disabled.

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.GetIntoTheQueue))]
public static class CheckoutManager_GetIntoTheQueue_Patch
{
    static void Postfix(CheckoutManager __instance, Customer customer)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning || !MultiplayerManager.IsHost) return;
        int netId = NpcNetworkManager.GetNetworkId(customer);
        if (netId == 0) return; // unregistered customer
        var w = NetMessages.WriteCheckoutCustomerQueued(__instance.FurnitureID, netId);
        MultiplayerManager.SendToAllReliable(w);
    }
}

// ── Customer places products on belt ────────────────────────────────────────
// Called by customer NPC AI after joining queue. Sends product types and each
// product's TempPrice so the client's ScanProduct() totals match the host.

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.PlaceProducts))]
public static class CheckoutManager_PlaceProducts_Patch
{
    static void Postfix(CheckoutManager __instance, List<Product> products)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning || !MultiplayerManager.IsHost) return;
        var types  = new int[products.Count];
        var prices = new float[products.Count];
        for (int i = 0; i < products.Count; i++)
        {
            types[i]  = (int)products[i].Data.type;
            prices[i] = HarmonyLib.Traverse.Create(products[i]).Field("tempPrice").GetValue<float>();
        }
        var w = NetMessages.WriteCheckoutProductsPlaced(__instance.FurnitureID, types, prices);
        MultiplayerManager.SendToAllReliable(w);
    }
}

// ── Customer leaves after paying ─────────────────────────────────────────────

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.OnLeaveStarted))]
public static class CheckoutManager_OnLeaveStarted_Patch
{
    static void Postfix(CheckoutManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning || !MultiplayerManager.IsHost) return;
        MultiplayerManager.SendToAllReliable(NetMessages.WriteCheckoutCustomerLeft(__instance.FurnitureID));
    }
}

// ── Manual player scan (non-automated desk) ──────────────────────────────────
// MoveToBag is private — patched by name.
// Only broadcasts for non-automated desks (automated ones use CheckoutStep above).
// The state field carries the pre-scan scannedProductCount (= product index).

[HarmonyPatch(typeof(CheckoutManager), "MoveToBag")]
public static class CheckoutManager_MoveToBag_Patch
{
    static void Prefix(CheckoutManager __instance, ref int __state)
    {
        __state = HarmonyLib.Traverse.Create(__instance).Field("scannedProductCount").GetValue<int>();
    }

    static void Postfix(CheckoutManager __instance, int __state)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        if (__instance.IsAutomated) return; // automated path handled by CheckoutStep patch
        var w = NetMessages.WriteCheckoutManualScan(__instance.FurnitureID, __state);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

// ── Customer offers cash payment ─────────────────────────────────────────────
// Called by customer NPC AI on host after all items scanned.

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.TakeCashPayment))]
public static class CheckoutManager_TakeCashPayment_Patch
{
    static void Postfix(CheckoutManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning || !MultiplayerManager.IsHost) return;
        MultiplayerManager.SendToAllReliable(NetMessages.WriteCheckoutPaymentCash(__instance.FurnitureID));
    }
}

// ── Customer offers card payment ─────────────────────────────────────────────

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.TakeCardPayment))]
public static class CheckoutManager_TakeCardPayment_Patch
{
    static void Postfix(CheckoutManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning || !MultiplayerManager.IsHost) return;
        MultiplayerManager.SendToAllReliable(NetMessages.WriteCheckoutPaymentCard(__instance.FurnitureID));
    }
}

// ── Player takes payment ─────────────────────────────────────────────────────
// When the CLIENT player presses Space to take payment, relay to host so the
// NPC transaction can complete (customer.PaymentDone → customer leaves).
// The money itself flows through EconomyPatches→MoneyUpdate, not duplicated here.

[HarmonyPatch(typeof(CheckoutManager), nameof(CheckoutManager.TakePayment))]
public static class CheckoutManager_TakePayment_Patch
{
    static void Postfix(CheckoutManager __instance)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteCheckoutPaymentTaken(__instance.FurnitureID);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllReliable(w);
        else                           MultiplayerManager.SendToHostReliable(w);
    }
}

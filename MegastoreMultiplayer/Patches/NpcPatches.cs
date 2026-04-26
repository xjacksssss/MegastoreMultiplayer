using HarmonyLib;
using MegastoreMultiplayer.Network;
using UnityEngine;

// ── Suppress client-side customer spawning ────────────────────────────────────

[HarmonyPatch(typeof(CustomerManager), nameof(CustomerManager.TrySpawnCustomer))]
public static class CustomerManager_TrySpawnCustomer_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

[HarmonyPatch(typeof(CustomerManager), "TrySpawnCarCustomer")]
public static class CustomerManager_TrySpawnCarCustomer_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

[HarmonyPatch(typeof(CustomerManager), "OnShopOpened")]
public static class CustomerManager_OnShopOpened_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

[HarmonyPatch(typeof(CustomerManager), "TrySpawnVendingCustomer")]
public static class CustomerManager_TrySpawnVendingCustomer_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

[HarmonyPatch(typeof(CustomerManager), "SpawnVendingRoutine")]
public static class CustomerManager_SpawnVendingRoutine_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

// ── Wandering people (ambient street NPCs) ────────────────────────────────────

[HarmonyPatch(typeof(WanderingPeopleManager), "SpawnNewWanderer")]
public static class WanderingPeopleManager_SpawnNewWanderer_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(WanderingPeopleManager __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        var people = Traverse.Create(__instance).Field("wanderingPeople").GetValue<System.Collections.Generic.List<CharacterMovement>>();
        var idx    = Traverse.Create(__instance).Field("index").GetValue<int>();
        if (people == null || people.Count == 0) return;
        int poolIdx   = (idx - 1 + people.Count) % people.Count;
        var character = people[poolIdx];
        if (character == null || !character.gameObject.activeSelf) return;
        var targetPos = Traverse.Create(character).Field("targetPosition").GetValue<Vector3>();
        NpcNetworkManager.RegisterWanderer(character, poolIdx, targetPos);
        MultiplayerManager.SendToAllReliable(
            NetMessages.WriteWandererSpawn(poolIdx, character.transform.position, character.transform.eulerAngles, targetPos));
    }
}

[HarmonyPatch(typeof(WanderingPeopleManager), "StopAndBackToPool")]
public static class WanderingPeopleManager_StopAndBackToPool_Patch
{
    static void Prefix(CharacterMovement character)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int poolIdx = NpcNetworkManager.GetWandererPoolIndex(character);
        if (poolIdx < 0) return;
        MultiplayerManager.SendToAllReliable(NetMessages.WriteWandererDespawn(poolIdx));
        NpcNetworkManager.UnregisterWanderer(poolIdx);
    }
}

// ── Customer Activate / Deactivate ────────────────────────────────────────────

[HarmonyPatch(typeof(Customer), nameof(Customer.Activate), new[] { typeof(Transform) })]
public static class Customer_Activate_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterCustomer(__instance);
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0)
            MultiplayerManager.SendToAllReliable(
                NetMessages.WriteNpcSpawn(netId, __instance.transform.position));
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.ActivateWithCar))]
public static class Customer_ActivateWithCar_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterCustomer(__instance);
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0)
            MultiplayerManager.SendToAllReliable(
                NetMessages.WriteNpcSpawn(netId, __instance.transform.position));
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.Deactivate))]
public static class Customer_Deactivate_Patch
{
    static void Prefix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0)
            MultiplayerManager.SendToAllReliable(NetMessages.WriteNpcDespawn(netId));
        NpcNetworkManager.UnregisterCustomer(__instance);
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.DeactivateReleaseAll))]
public static class Customer_DeactivateReleaseAll_Patch
{
    static void Prefix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0)
            MultiplayerManager.SendToAllReliable(NetMessages.WriteNpcDespawn(netId));
        NpcNetworkManager.UnregisterCustomer(__instance);
    }
}

// ── Customer animation events (host → clients) ────────────────────────────────

[HarmonyPatch(typeof(Customer), nameof(Customer.GetProductToHand))]
public static class Customer_GetProductToHand_Patch
{
    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteCustomerAnim(netId, "pickup0"));
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.GetProductToBag))]
public static class Customer_GetProductToBag_Patch
{
    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteCustomerAnim(netId, "pickup0"));
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.OnPickupFinished))]
public static class Customer_OnPickupFinished_Patch
{
    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteCustomerAnim(netId, "idle0"));
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.OnLookAroundFinished))]
public static class Customer_OnLookAroundFinished_Patch
{
    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteCustomerAnim(netId, "idle0"));
    }
}

[HarmonyPatch(typeof(Customer), nameof(Customer.OnComplainFinished))]
public static class Customer_OnComplainFinished_Patch
{
    static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteCustomerAnim(netId, "idle0"));
    }
}

// Customer complaint START animation — method name verified via dnSpy; candidates are
// probed safely in Plugin.TryPatchCustomerComplain() to avoid a PatchAll crash.
public static class Customer_Complain_Patch
{
    public static void Postfix(Customer __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int netId = NpcNetworkManager.GetNetworkId(__instance);
        if (netId > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteCustomerAnim(netId, "complain0"));
    }
}

// ── Employee AI suppression on clients ────────────────────────────────────────

[HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.DeactivateAllEmployeesInstant))]
public static class EmployeeManager_DeactivateAllInstant_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

[HarmonyPatch(typeof(Employee), nameof(Employee.TryDeactivate))]
public static class Employee_TryDeactivate_Patch
{
    static bool Prefix(Employee __instance)
    {
        if (MultiplayerManager.IsClient) return false; // clients: block, host manages visibility

        if (MultiplayerManager.IsHost)
        {
            int id = __instance.GetEmployeeID();
            if (id > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteEmployeeDeactivated(id));
            NpcNetworkManager.UnregisterEmployee(__instance);
        }
        return true;
    }
}

[HarmonyPatch(typeof(Employee), nameof(Employee.DeactivateInstant))]
public static class Employee_DeactivateInstant_Patch
{
    static bool Prefix(Employee __instance)
    {
        if (MultiplayerManager.IsClient) return false;

        if (MultiplayerManager.IsHost)
        {
            int id = __instance.GetEmployeeID();
            if (id > 0) MultiplayerManager.SendToAllReliable(NetMessages.WriteEmployeeDeactivated(id));
            NpcNetworkManager.UnregisterEmployee(__instance);
        }
        return true;
    }
}

// ── Employee Activate patches (all 5 types) ───────────────────────────────────
// Prefix returns false on clients (blocks AI).
// Postfix registers with NpcNetworkManager and broadcasts EmployeeActivated on host.

[HarmonyPatch(typeof(Baker), nameof(Baker.Activate), new[] { typeof(HiringManager.EmployeeStats) })]
public static class Baker_Activate_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(Baker __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterEmployee(__instance);
        MultiplayerManager.SendToAllReliable(
            NetMessages.WriteEmployeeActivated(__instance.GetEmployeeID(), __instance.transform.position));
    }
}

[HarmonyPatch(typeof(Cashier), nameof(Cashier.Activate), new[] { typeof(HiringManager.EmployeeStats), typeof(int) })]
public static class Cashier_Activate_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(Cashier __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterEmployee(__instance);
        MultiplayerManager.SendToAllReliable(
            NetMessages.WriteEmployeeActivated(__instance.GetEmployeeID(), __instance.transform.position));
    }
}

[HarmonyPatch(typeof(Restocker), nameof(Restocker.Activate), new[] { typeof(HiringManager.EmployeeStats) })]
public static class Restocker_Activate_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(Restocker __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterEmployee(__instance);
        MultiplayerManager.SendToAllReliable(
            NetMessages.WriteEmployeeActivated(__instance.GetEmployeeID(), __instance.transform.position));
    }
}

[HarmonyPatch(typeof(Unloader), nameof(Unloader.Activate), new[] { typeof(HiringManager.EmployeeStats) })]
public static class Unloader_Activate_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(Unloader __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterEmployee(__instance);
        MultiplayerManager.SendToAllReliable(
            NetMessages.WriteEmployeeActivated(__instance.GetEmployeeID(), __instance.transform.position));
    }
}

[HarmonyPatch(typeof(SeafoodStaff), nameof(SeafoodStaff.Activate), new[] { typeof(HiringManager.EmployeeStats) })]
public static class SeafoodStaff_Activate_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;

    static void Postfix(SeafoodStaff __instance)
    {
        if (!MultiplayerManager.IsHost) return;
        NpcNetworkManager.RegisterEmployee(__instance);
        MultiplayerManager.SendToAllReliable(
            NetMessages.WriteEmployeeActivated(__instance.GetEmployeeID(), __instance.transform.position));
    }
}

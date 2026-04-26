using HarmonyLib;
using MegastoreMultiplayer.Network;

// ── Block client-side car spawning ───────────────────────────────────────────
// Clients must not run their own car simulation — all car movement is driven
// by the host and streamed via CarSpawn / CarPosition / CarDespawn packets.

[HarmonyPatch(typeof(CarCustomerManager), nameof(CarCustomerManager.SpawnCarCustomer))]
public static class CarCustomerManager_SpawnCarCustomer_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

[HarmonyPatch(typeof(CarCustomerManager), nameof(CarCustomerManager.TrySpawnWaitingCar))]
public static class CarCustomerManager_TrySpawnWaitingCar_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

// ── Car enters parking lot (host only) ───────────────────────────────────────
// SetParkingLotIndex is called when a car is assigned a parking spot.
// ParkingLotIndex acts as the stable ID for this car's session.

[HarmonyPatch(typeof(CustomerCar), nameof(CustomerCar.SetParkingLotIndex))]
public static class CustomerCar_SetParkingLotIndex_Patch
{
    static void Postfix(CustomerCar __instance, int index)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        if (index < 0) return; // -1 means the car is being returned to pool
        NpcNetworkManager.RegisterCar(__instance, index);
        var w = NetMessages.WriteCarSpawn(index, __instance.transform.position, __instance.transform.eulerAngles);
        MultiplayerManager.SendToAllReliable(w);
    }
}

// ── Car leaves parking lot (host only) ───────────────────────────────────────

[HarmonyPatch(typeof(CarCustomerManager), nameof(CarCustomerManager.LeaveParkingLot))]
public static class CarCustomerManager_LeaveParkingLot_Patch
{
    static void Prefix(CustomerCar car)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        NpcNetworkManager.UnregisterCar(car.ParkingLotIndex);
        MultiplayerManager.SendToAllReliable(NetMessages.WriteCarDespawn(car.ParkingLotIndex));
    }
}

// ── Block CarAI movement on clients ──────────────────────────────────────────
// The host drives the car via Move(); on clients we suppress it and apply
// positions directly from CarPosition packets.

[HarmonyPatch(typeof(CarAI), nameof(CarAI.Move))]
public static class CarAI_Move_Patch
{
    static bool Prefix() => !MultiplayerManager.IsClient;
}

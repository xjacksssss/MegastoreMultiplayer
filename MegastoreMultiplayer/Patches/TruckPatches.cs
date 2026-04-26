using HarmonyLib;
using MegastoreMultiplayer.Network;

// Syncs delivery truck arrivals and departures.
// TruckManager.truckDictionary maps OrderReceivingArea → Truck, giving us a
// stable area index to use as the truck identifier.

[HarmonyPatch(typeof(Truck), nameof(Truck.Activate))]
public static class Truck_Activate_Patch
{
    static bool Prefix() => !(MultiplayerManager.IsClient && !NetApply.IsApplying);

    static void Postfix(Truck __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int area = TruckHelper.GetAreaForTruck(__instance);
        if (area < 0) return;
        NpcNetworkManager.RegisterTruck(area);
        MultiplayerManager.SendToAllReliable(NetMessages.WriteTruckArrived(area));
    }
}

[HarmonyPatch(typeof(Truck), nameof(Truck.Leave))]
public static class Truck_Leave_Patch
{
    static bool Prefix() => !(MultiplayerManager.IsClient && !NetApply.IsApplying);

    static void Postfix(Truck __instance)
    {
        if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
        int area = TruckHelper.GetAreaForTruck(__instance);
        if (area < 0) return;
        NpcNetworkManager.UnregisterTruck(area);
        MultiplayerManager.SendToAllReliable(NetMessages.WriteTruckLeft(area));
    }
}

internal static class TruckHelper
{
    internal static int GetAreaForTruck(Truck truck)
    {
        var tm = SingletonBehaviour<TruckManager>.Instance;
        if (tm == null) return -1;
        var dict = HarmonyLib.Traverse.Create(tm)
            .Field("truckDictionary")
            .GetValue<TruckManager.TruckDictionary>();
        if (dict == null) return -1;
        foreach (OrderManager.OrderReceivingArea area in System.Enum.GetValues(typeof(OrderManager.OrderReceivingArea)))
        {
            if (dict.TryGetValue(area, out var t) && t == truck)
                return (int)area;
        }
        return -1;
    }
}

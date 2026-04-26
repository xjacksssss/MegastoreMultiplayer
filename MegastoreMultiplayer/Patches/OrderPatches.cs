using System.Collections.Generic;
using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(OrderManager), nameof(OrderManager.AddOrder))]
public static class OrderManager_AddOrder_Patch
{
    static void Postfix(List<BuyPanel.CartSlot> orders, OrderManager.OrderReceivingArea receivingArea)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;

        var types    = new int[orders.Count];
        var counts   = new int[orders.Count];
        var isPallet = new bool[orders.Count];
        for (int i = 0; i < orders.Count; i++)
        {
            types[i]    = (int)orders[i].type;
            counts[i]   = orders[i].amount;
            isPallet[i] = orders[i].isPallet;
        }

        var w = NetMessages.WriteOrderConfirmed((int)receivingArea, types, counts, isPallet);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

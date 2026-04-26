using HarmonyLib;
using MegastoreMultiplayer.Network;
using UnityEngine;

// Broadcasts local player position (and driven vehicle position) at 20 Hz.
[HarmonyPatch(typeof(PlayerMove), "FixedUpdate")]
public static class PlayerMove_FixedUpdate_Patch
{
    private static float _lastSend;
    private const float Interval = 0.05f; // 20 Hz

    static void Postfix(PlayerMove __instance)
    {
        if (!MultiplayerManager.IsRunning) return;

        float now = Time.time;
        if (now - _lastSend < Interval) return;
        _lastSend = now;

        Vector3 pos   = __instance.transform.position;
        Vector3 euler = SingletonBehaviour<PlayerLook>.Instance.transform.eulerAngles;

        var pw = NetMessages.WritePlayerPosition(pos, euler);
        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllUnreliable(pw);
        else                           MultiplayerManager.SendToHostUnreliable(pw);

        // Also broadcast the position of any vehicle the local player is driving.
        var vm = SingletonBehaviour<VehicleManager>.Instance;
        if (vm == null || !vm.IsOnVehicle) return;

        var vehicle = vm.TakenVehicle;
        var vw = NetMessages.WriteVehiclePosition(
            (int)vehicle.VehicleType,
            vehicle.VehicleID,
            vehicle.transform.position,
            vehicle.transform.eulerAngles);

        if (MultiplayerManager.IsHost) MultiplayerManager.SendToAllUnreliable(vw);
        else                           MultiplayerManager.SendToHostUnreliable(vw);
    }
}

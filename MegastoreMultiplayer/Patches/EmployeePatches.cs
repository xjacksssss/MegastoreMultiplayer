using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.HireEmployee))]
public static class EmployeeManager_HireEmployee_Patch
{
    static void Postfix(HiringManager.EmployeeStats employeeStats)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteEmployeeHired(employeeStats);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.FireEmployee))]
public static class EmployeeManager_FireEmployee_Patch
{
    static void Postfix(HiringManager.EmployeeStats employeeStats)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteEmployeeFired(employeeStats.employeeID);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.UpdateEmployeeData))]
public static class EmployeeManager_UpdateEmployeeData_Patch
{
    static void Postfix(HiringManager.EmployeeStats employeeStats)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteEmployeeUpdated(employeeStats);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

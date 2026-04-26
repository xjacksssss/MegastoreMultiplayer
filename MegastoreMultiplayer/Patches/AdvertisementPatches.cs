using HarmonyLib;
using MegastoreMultiplayer.Network;

// Syncs the in-game offer/promotion system (OfferManager) across clients.
// OfferManager is a SingletonBehaviour that controls limited-time product offers.

[HarmonyPatch(typeof(OfferManager), nameof(OfferManager.ActivateOffer))]
public static class OfferManager_ActivateOffer_Patch
{
    static void Postfix()
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteAdUpdate(true);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

[HarmonyPatch(typeof(OfferManager), "DeactivateOffer")]
public static class OfferManager_DeactivateOffer_Patch
{
    static void Postfix()
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteAdUpdate(false);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

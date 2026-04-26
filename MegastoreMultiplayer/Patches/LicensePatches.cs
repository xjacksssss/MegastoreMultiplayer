using HarmonyLib;
using MegastoreMultiplayer.Network;

[HarmonyPatch(typeof(ProductLicenseManager), nameof(ProductLicenseManager.PurchaseLicense))]
public static class ProductLicenseManager_PurchaseLicense_Patch
{
    static void Postfix(int licenseLevel, ProductGroup productGroup, bool defaultPurchase)
    {
        if (defaultPurchase) return;
        LicenseTracker.Record(licenseLevel, (int)productGroup);
        if (!MultiplayerManager.IsRunning) return;
        if (NetApply.IsApplying) return;
        var w = NetMessages.WriteLicensePurchased(licenseLevel, (int)productGroup);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

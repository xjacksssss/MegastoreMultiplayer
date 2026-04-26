using HarmonyLib;
using MegastoreMultiplayer.Network;

// StockManager.Initialize() is called once the game world is fully loaded.
// If a StateSnapshot arrived while we were still on the main menu, apply it now.
// ShelfRegistry is already populated at this point because Shelf.Initialize()
// fires before StockManager.Initialize() during scene setup.
[HarmonyPatch(typeof(StockManager), nameof(StockManager.Initialize))]
public static class StockManager_Initialize_Patch
{
    static void Postfix()
    {
        StateSnapshot.ApplyPendingIfAny();
    }
}

// For clients who have never launched the game before, StartWindow would block
// with a store-setup dialog. Suppress it — the host's StateSnapshot is the authority.
[HarmonyPatch(typeof(StartWindow), nameof(StartWindow.Initialize))]
public static class StartWindow_Initialize_Client_Patch
{
    static void Prefix()
    {
        if (!MultiplayerManager.IsClient) return;
        // Ensure the key exists so Initialize() sees it and skips the dialog.
        if (!GenericDataSerializer.HasKey("INITIAL_PRDUCTGROUP_KEY"))
            GenericDataSerializer.Save("INITIAL_PRDUCTGROUP_KEY", ProductGroup.GROCERY);
    }
}

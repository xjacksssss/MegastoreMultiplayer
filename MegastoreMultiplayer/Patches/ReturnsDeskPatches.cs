using DG.Tweening;
using HarmonyLib;
using MegastoreMultiplayer.Network;

// Returns desk — sync coverage summary:
//   Box position  → ReturnArea_PlaceBox_Patch (below) broadcasts BoxDropped.
//   Refund money  → EconomyManager.RemoveSoftCurrency is already patched (EconomyPatches)
//                   and fires MoneyUpdate automatically — no extra work needed here.
//   Shelf restock → Shelf.PlaceProduct / RemoveProduct patched (ShelfPatches) — covered.
// Nothing else needs to be added for the returns flow.

[HarmonyPatch(typeof(ReturnArea), nameof(ReturnArea.PlaceBox))]
public static class ReturnArea_PlaceBox_Patch
{
    static void Postfix(Box box, bool instant)
    {
        if (!MultiplayerManager.IsRunning || NetApply.IsApplying || !MultiplayerManager.IsHost) return;
        if (box == null) return;

        if (instant)
        {
            var w = NetMessages.WriteBoxDropped(box.BoxID, box.transform.position, box.transform.eulerAngles);
            MultiplayerManager.SendToAllReliable(w);
        }
        else
        {
            // Broadcast after the 0.3 s DOTween placement animation finishes.
            DOVirtual.DelayedCall(0.35f, () =>
            {
                if (box == null) return;
                var w = NetMessages.WriteBoxDropped(box.BoxID, box.transform.position, box.transform.eulerAngles);
                MultiplayerManager.SendToAllReliable(w);
            });
        }
    }
}

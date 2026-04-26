namespace MegastoreMultiplayer.Network
{
    // Called when the client's peer connection to the host is established.
    // We do NOT trigger a game load here — that happens inside SaveDataSync.Apply()
    // once the host's save files have been written to disk.
    public static class ClientJoinFlow
    {
        public static void OnConnected()
        {
            Plugin.Log.LogInfo("[Join] Connected. Waiting for host save data...");
            // SaveDataSync message arrives next (ReliableOrdered guarantees order).
            // SaveDataSync.Apply() writes the files then calls OnContinueButtonClicked().
        }
    }
}

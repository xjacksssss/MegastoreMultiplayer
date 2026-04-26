using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Host computes a lightweight hash every 30 s and broadcasts it.
    // Clients compare their local hash and log a warning (+ show UI flag) on mismatch.
    // Call Tick() from Plugin.Update().
    public static class DesyncDetector
    {
        private const float CheckInterval = 30f;
        private static float _timer;

        public static bool DesyncDetected { get; private set; }

        public static void Tick()
        {
            if (!MultiplayerManager.IsRunning) return;

            if (MultiplayerManager.IsHost)
            {
                _timer += Time.deltaTime;
                if (_timer >= CheckInterval)
                {
                    _timer = 0f;
                    int hash = ComputeHash();
                    MultiplayerManager.SendToAllReliable(NetMessages.WriteSyncCheck(hash));
                }
            }
        }

        // Called by NetMessages when a SyncCheck packet arrives on a client.
        public static void CheckHash(int hostHash)
        {
            int localHash = ComputeHash();
            if (localHash == hostHash)
            {
                DesyncDetected = false;
                return;
            }

            DesyncDetected = true;
            Plugin.Log.LogWarning(
                $"[Desync] Hash mismatch — host={hostHash}, local={localHash}. " +
                "Use the Resync button in the multiplayer menu to re-sync.");
        }

        // Called when the player clicks Resync in the UI.
        public static void RequestResync()
        {
            if (!MultiplayerManager.IsClient) return;
            MultiplayerManager.SendToHostReliable(NetMessages.WriteResyncRequest());
            DesyncDetected = false;
            Plugin.Log.LogInfo("[Desync] Resync requested.");
        }

        public static void Reset()
        {
            _timer         = 0f;
            DesyncDetected = false;
        }

        // Hash = money-as-int XOR total-shelf-products XOR total-box-count.
        // Fast and catches the most common desyncs (money and stock).
        private static int ComputeHash()
        {
            int hash = 0;

            var em = SingletonBehaviour<EconomyManager>.Instance;
            if (em != null)
                hash ^= Mathf.RoundToInt(em.SoftCurrency);

            int shelfTotal = 0;
            foreach (var shelf in ShelfRegistry.All)
                if (shelf != null) shelfTotal += shelf.GetProductCount();
            hash ^= shelfTotal * 31;

            var boxes = StateSnapshot.GetSpawnedBoxes();
            if (boxes != null)
                hash ^= boxes.Count * 7;

            return hash;
        }
    }
}

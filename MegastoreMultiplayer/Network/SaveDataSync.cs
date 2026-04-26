using System.IO;
using System.Reflection;
using HarmonyLib;
using LiteNetLib.Utils;
using MegastoreMultiplayer.Messages;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Sends the host's active save file to a joining client.
    // The client writes it to a dedicated multiplayer slot (index 99, file Save_99.data)
    // then tells DataSerializer to use that slot. The client's real saves (0-3) are
    // never touched. On disconnect, Save_99.data is deleted and the original slot is
    // restored.
    //
    // Wire-format:
    //   byte  MessageType.SaveDataSync
    //   int   hostProfileIndex   (which Save_N.data the host is playing on)
    //   int   byteCount
    //   bytes raw Save_N.data bytes
    public static class SaveDataSync
    {
        public const int MultiplayerSlot = 99;

        private static readonly NetDataWriter _writer = new NetDataWriter();
        private static int _clientOriginalSlot = 0;

        // DataSerializer is in ToolBox.Serialization — accessed via reflection because
        // we don't have a compile-time reference to that namespace.
        private static readonly System.Type _ds =
            AccessTools.TypeByName("ToolBox.Serialization.DataSerializer");

        // ── Build (host side) ────────────────────────────────────────────────────

        public static NetDataWriter Build()
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.SaveDataSync);

            int    slot     = GetCurrentSlot();
            string savePath = SlotPath(slot);

            byte[] data = File.Exists(savePath) ? ReadFileSafe(savePath) : new byte[0];

            _writer.Put(slot);
            _writer.Put(data.Length);
            if (data.Length > 0)
                _writer.Put(data, 0, data.Length);

            Plugin.Log.LogInfo($"[SaveSync] Built: slot {slot}, {data.Length / 1024} KB.");
            return _writer;
        }

        // ── Apply (client side) ──────────────────────────────────────────────────

        public static void Apply(NetDataReader r)
        {
            int    hostSlot = r.GetInt();
            int    length   = r.GetInt();
            byte[] data     = new byte[length];
            if (length > 0)
                r.GetBytes(data, length);

            // Remember where the client was before we hijack their profile.
            _clientOriginalSlot = GetCurrentSlot();

            // Write host's save data into the throwaway multiplayer slot.
            string tempPath = SlotPath(MultiplayerSlot);
            if (length > 0)
                File.WriteAllBytes(tempPath, data);

            // Tell DataSerializer to read/write slot 99 from now on.
            ChangeProfile(MultiplayerSlot);

            Plugin.Log.LogInfo($"[SaveSync] Applied host slot {hostSlot} → MP slot {MultiplayerSlot} ({length / 1024} KB). Client original slot: {_clientOriginalSlot}.");

            // If the game scene is already loaded (mid-session reconnect) skip the scene reload —
            // the StateSnapshot that follows on the reliable channel will re-sync all state.
            if (SingletonBehaviour<StockManager>.Instance != null)
            {
                Plugin.Log.LogInfo("[SaveSync] Game already loaded — skipping scene reload; StateSnapshot will re-sync.");
                return;
            }

            // First-time join: trigger the game scene load now that the correct save is in place.
            var loadScreen = Object.FindAnyObjectByType<LoadScreen>();
            if (loadScreen != null)
            {
                Plugin.Log.LogInfo("[SaveSync] Triggering game scene load...");
                loadScreen.OnContinueButtonClicked();
            }
        }

        // Called by MultiplayerManager.Stop() — remove temp file, restore client slot.
        public static void Cleanup()
        {
            string tempPath = SlotPath(MultiplayerSlot);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                Plugin.Log.LogInfo($"[SaveSync] Deleted temp save {tempPath}.");
            }

            ChangeProfile(_clientOriginalSlot);
            Plugin.Log.LogInfo($"[SaveSync] Restored client to slot {_clientOriginalSlot}.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string SlotPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"Save_{slot}.data");

        private static int GetCurrentSlot()
        {
            if (_ds == null) return 0;
            try
            {
                // DataSerializer.LoadLastProfileIndex() returns -1 if none set; default to 0.
                object result = _ds.GetMethod("LoadLastProfileIndex",
                    BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                int idx = result is int i ? i : 0;
                return idx < 0 ? 0 : idx;
            }
            catch { return 0; }
        }

        private static void ChangeProfile(int slot)
        {
            if (_ds == null) return;
            try
            {
                _ds.GetMethod("ChangeProfile",
                    BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { slot });
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[SaveSync] ChangeProfile({slot}) failed: {ex.Message}");
            }
        }

        private static byte[] ReadFileSafe(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] buf = new byte[fs.Length];
                    fs.Read(buf, 0, buf.Length);
                    return buf;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[SaveSync] Could not read {path}: {ex.Message}");
                return new byte[0];
            }
        }
    }
}

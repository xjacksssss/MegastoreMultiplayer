using System.Collections.Generic;

namespace MegastoreMultiplayer.Network
{
    // Populated by patching Tray.Initialize so the network layer can look up a
    // Tray by its stable TrayID (assigned once and saved to disk).
    public static class TrayRegistry
    {
        private static readonly Dictionary<int, Tray> _trays = new Dictionary<int, Tray>();

        public static void Register(int trayId, Tray tray) => _trays[trayId] = tray;
        public static Tray Get(int trayId) => _trays.TryGetValue(trayId, out var t) ? t : null;
        public static IEnumerable<Tray> All => _trays.Values;

        public static void Clear() => _trays.Clear();
    }
}

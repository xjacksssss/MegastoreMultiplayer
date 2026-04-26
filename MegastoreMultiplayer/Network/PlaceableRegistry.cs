using System.Collections.Generic;

namespace MegastoreMultiplayer.Network
{
    public static class PlaceableRegistry
    {
        private static readonly Dictionary<string, Placeable> _map = new Dictionary<string, Placeable>();

        public static void Register(Placeable p) => _map[Key(p)] = p;

        public static Placeable Get(PlaceableType type, int id) =>
            _map.TryGetValue(Key(type, id), out var p) ? p : null;

        public static string Key(Placeable p)    => p.Type.ToString() + p.PlaceableID;
        public static string Key(PlaceableType t, int id) => t.ToString() + id;

        public static IEnumerable<Placeable> All => _map.Values;

        public static void Clear() => _map.Clear();
    }
}

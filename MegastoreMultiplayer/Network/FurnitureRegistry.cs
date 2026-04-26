using System.Collections.Generic;

namespace MegastoreMultiplayer.Network
{
    public static class FurnitureRegistry
    {
        private static readonly Dictionary<string, Furniture> _map = new Dictionary<string, Furniture>();

        public static void Register(Furniture f) => _map[Key(f)] = f;

        public static Furniture Get(FurnitureType type, int id) =>
            _map.TryGetValue(Key(type, id), out var f) ? f : null;

        public static string Key(Furniture f)             => f.Type.ToString() + f.FurnitureID;
        public static string Key(FurnitureType t, int id) => t.ToString() + id;

        public static IEnumerable<Furniture> All => _map.Values;

        public static void Clear() => _map.Clear();
    }
}

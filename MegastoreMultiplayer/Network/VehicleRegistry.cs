using System.Collections.Generic;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Lazy cache — rebuilt on first access after a scene change.
    // FindObjectsOfType is only called once per scene, not every frame.
    public static class VehicleRegistry
    {
        private static Dictionary<string, VehichleInteractable> _cache;

        public static VehichleInteractable Get(VehicleType type, int id)
        {
            RebuildIfNeeded();
            _cache.TryGetValue(Key(type, id), out var v);
            return v;
        }

        public static IEnumerable<VehichleInteractable> All
        {
            get { RebuildIfNeeded(); return _cache.Values; }
        }

        public static string Key(VehicleType type, int id) => type.ToString() + id;

        // Call when a new scene is loaded so the cache is rebuilt against current objects.
        public static void Invalidate() => _cache = null;

        private static void RebuildIfNeeded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, VehichleInteractable>();
            foreach (var v in Object.FindObjectsByType<VehichleInteractable>(FindObjectsSortMode.None))
                _cache[Key(v.VehicleType, v.VehicleID)] = v;
        }
    }
}

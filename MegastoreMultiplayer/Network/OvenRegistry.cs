using System.Collections.Generic;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    public static class OvenRegistry
    {
        private static readonly Dictionary<int, Oven>  _ovens      = new Dictionary<int, Oven>();
        private static readonly Dictionary<int, float> _startTimes = new Dictionary<int, float>();

        public static void Register(Oven oven) => _ovens[oven.FurnitureID] = oven;
        public static void Unregister(Oven oven) => _ovens.Remove(oven.FurnitureID);
        public static Oven Get(int id) => _ovens.TryGetValue(id, out var o) ? o : null;
        public static IEnumerable<Oven> All => _ovens.Values;

        public static void RecordStart(int ovenId) => _startTimes[ovenId] = Time.time;
        public static void ClearStart(int ovenId)  => _startTimes.Remove(ovenId);
        public static float GetElapsed(int ovenId) =>
            _startTimes.TryGetValue(ovenId, out float t) ? Time.time - t : 0f;

        public static void Clear()
        {
            _ovens.Clear();
            _startTimes.Clear();
        }
    }
}

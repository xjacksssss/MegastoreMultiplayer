using System.Collections.Generic;

namespace MegastoreMultiplayer.Network
{
    // Built at runtime by patching Shelf.Initialize(). Lets the network layer look
    // up a Shelf by its stable network ID (the same string the game uses as a save key).
    public static class ShelfRegistry
    {
        private static readonly Dictionary<string, Shelf> _shelves = new Dictionary<string, Shelf>();

        public static void Register(Shelf shelf)
        {
            string id = GetId(shelf);
            _shelves[id] = shelf;
        }

        public static Shelf Get(string id)
        {
            _shelves.TryGetValue(id, out var shelf);
            return shelf;
        }

        public static IEnumerable<Shelf> All => _shelves.Values;

        public static string GetId(Shelf shelf) =>
            shelf.ParentPlaceable.Type.ToString() + shelf.ParentPlaceable.PlaceableID + "|" + shelf.ShelfID;

        public static void Clear() => _shelves.Clear();
    }
}

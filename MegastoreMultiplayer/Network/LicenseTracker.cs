using System.Collections.Generic;

namespace MegastoreMultiplayer.Network
{
    // Records every ProductLicenseManager.PurchaseLicense(level, group) call that
    // occurs during a session so StateSnapshot can replay them for joining clients.
    public static class LicenseTracker
    {
        private static readonly List<(int level, int group)> _purchased =
            new List<(int, int)>();

        public static void Record(int level, int group)
        {
            var entry = (level, group);
            if (!_purchased.Contains(entry))
                _purchased.Add(entry);
        }

        public static IReadOnlyList<(int level, int group)> All => _purchased;

        public static void Clear() => _purchased.Clear();
    }
}

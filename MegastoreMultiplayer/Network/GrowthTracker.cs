namespace MegastoreMultiplayer.Network
{
    // Records how many GrowthManager.PurchaseGrowth() calls have occurred so that
    // StateSnapshot can replay the same number of expansions on a joining client.
    public static class GrowthTracker
    {
        public static int Count { get; private set; }

        public static void Record() => Count++;

        public static void Clear() => Count = 0;
    }
}

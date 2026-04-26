using System;

namespace MegastoreMultiplayer.Network
{
    // Guards all Harmony patches so they don't re-broadcast when we're applying
    // remote state locally. Wrap any game-state call triggered by the network with
    // NetApply.Run(() => ...) and check NetApply.IsApplying in each patch Postfix.
    public static class NetApply
    {
        public static bool IsApplying { get; private set; }

        public static void Run(Action action)
        {
            IsApplying = true;
            try   { action(); }
            finally { IsApplying = false; }
        }
    }
}

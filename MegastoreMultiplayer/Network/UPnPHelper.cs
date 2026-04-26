using System;
using System.Threading;
using System.Threading.Tasks;
using Open.Nat;

namespace MegastoreMultiplayer.Network
{
    public enum UPnPStatus { Idle, Discovering, Mapped, Failed }

    // Attempts to auto-map a UDP port on the host's router via UPnP so players
    // don't have to port-forward manually.  All network I/O is fire-and-forget on
    // a background thread; poll Status / ExternalIp from the UI every frame.
    public static class UPnPHelper
    {
        public static UPnPStatus Status     { get; private set; } = UPnPStatus.Idle;
        public static string     ExternalIp { get; private set; }

        private static NatDevice _device;
        private static Mapping   _mapping;

        public static void TryMap(int port)
        {
            Status     = UPnPStatus.Discovering;
            ExternalIp = null;
            _device    = null;
            _mapping   = null;

            Task.Run(async () =>
            {
                try
                {
                    var discoverer = new NatDiscoverer();
                    // 6-second window; most routers respond in < 1 s.
                    var cts    = new CancellationTokenSource(6000);
                    var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                    var map = new Mapping(Protocol.Udp, port, port, 0, "MegastoreMultiplayer");
                    await device.CreatePortMapAsync(map);

                    var ip = await device.GetExternalIPAsync();

                    _device    = device;
                    _mapping   = map;
                    ExternalIp = ip.ToString();
                    Status     = UPnPStatus.Mapped;

                    Plugin.Log.LogInfo($"[UPnP] Port {port}/UDP mapped. External IP: {ExternalIp}");
                }
                catch (NatDeviceNotFoundException)
                {
                    Status = UPnPStatus.Failed;
                    Plugin.Log.LogWarning("[UPnP] No UPnP router found — players must port-forward 7777 UDP manually.");
                }
                catch (Exception ex)
                {
                    Status = UPnPStatus.Failed;
                    Plugin.Log.LogWarning($"[UPnP] Failed ({ex.Message}) — players must port-forward 7777 UDP manually.");
                }
            });
        }

        public static void Release()
        {
            Status     = UPnPStatus.Idle;
            ExternalIp = null;

            var device  = _device;
            var mapping = _mapping;
            _device  = null;
            _mapping = null;

            if (device == null || mapping == null) return;

            Task.Run(async () =>
            {
                try   { await device.DeletePortMapAsync(mapping); Plugin.Log.LogInfo("[UPnP] Port mapping removed."); }
                catch { /* best-effort cleanup */ }
            });
        }
    }
}

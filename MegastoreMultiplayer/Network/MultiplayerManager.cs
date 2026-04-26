using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using MegastoreMultiplayer.Messages;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Central networking singleton. Call Poll() every frame from Plugin.Update().
    // One player runs as host (server + local client); others join as pure clients.
    public static class MultiplayerManager
    {
        public const int DefaultPort = 7777;
        private const string ConnectionKey = "megastore_mm";

        public static bool IsRunning { get; private set; }
        public static bool IsHost    { get; private set; }
        public static bool IsClient  => IsRunning && !IsHost;

        // Connected peers (host-side only — clients have a single _hostPeer instead)
        public static IReadOnlyList<NetPeer> Clients => _clients;
        private static readonly List<NetPeer> _clients = new List<NetPeer>();
        private static NetPeer _hostPeer;

        private static NetManager _server;
        private static NetManager _client;

        // Port saved on Start/Join so migration/reconnect can reuse it.
        public static int    LastPort   { get; private set; } = DefaultPort;
        public static string LastHostIp { get; private set; }

        // Migration state stored by clients when host sends MigrationPlan.
        private static string _migrationTargetIp;
        private static bool   _isMigrationTarget;
        private static float  _migrationReconnectTimer = -1f;

        // Reconnect-on-drop state (non-migration disconnects).
        private const int   MaxReconnectAttempts = 3;
        private static int   _reconnectAttempts;
        private static float _reconnectTimer = -1f;

        public static event Action<NetPeer> OnClientConnected;
        public static event Action<NetPeer> OnClientDisconnected;
        public static event Action          OnConnectedToHost;
        public static event Action          OnDisconnectedFromHost;

        // ── Host ────────────────────────────────────────────────────────────────

        public static void StartHost(int port = DefaultPort)
        {
            if (IsRunning) Stop();

            var listener = new EventBasedNetListener();
            _server = new NetManager(listener) { AutoRecycle = true };

            listener.ConnectionRequestEvent += req => req.AcceptIfKey(ConnectionKey);
            listener.PeerConnectedEvent     += OnServerPeerConnected;
            listener.PeerDisconnectedEvent  += OnServerPeerDisconnected;
            listener.NetworkReceiveEvent    += OnServerReceive;

            if (!_server.Start(port))
            {
                Plugin.Log.LogError($"[Net] Failed to bind on port {port}.");
                return;
            }

            LastPort  = port;
            IsRunning = true;
            IsHost    = true;
            Plugin.Log.LogInfo($"[Net] Hosting on port {port}.");
            UPnPHelper.TryMap(port);
        }

        private static void OnServerPeerConnected(NetPeer peer)
        {
            _clients.Add(peer);
            Plugin.Log.LogInfo($"[Net] Client connected: {peer.Address} (id={peer.Id})");
            RemotePlayerManager.AddPlayer(peer);
            OnClientConnected?.Invoke(peer);

            // Send save data then live state snapshot.
            peer.Send(SaveDataSync.Build(), DeliveryMethod.ReliableOrdered);
            peer.Send(StateSnapshot.Build(), DeliveryMethod.ReliableOrdered);

            // Tell all clients the updated migration plan.
            BroadcastMigrationPlan();
        }

        private static void OnServerPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            _clients.Remove(peer);
            Plugin.Log.LogInfo($"[Net] Client disconnected: {peer.Address} ({info.Reason})");

            // Drop any boxes the peer was carrying so they don't stay invisible on
            // clients that received them as !isStored in the snapshot.
            ReleaseCarriedBoxes(peer.Id);

            RemotePlayerManager.RemovePlayer(peer);
            OnClientDisconnected?.Invoke(peer);

            // Re-broadcast so remaining clients know the new migration target.
            BroadcastMigrationPlan();
        }

        private static void ReleaseCarriedBoxes(int peerId)
        {
            var carriedIds = new System.Collections.Generic.List<int>(NetMessages.GetBoxesCarriedBy(peerId));
            if (carriedIds.Count == 0) return;

            var allBoxes = StateSnapshot.GetSpawnedBoxes();
            foreach (int boxId in carriedIds)
            {
                NetMessages.UnlockBox(boxId);
                if (allBoxes == null || !allBoxes.TryGetValue(boxId, out var box) || box == null) continue;
                // Reactivate locally on the host and tell all remaining clients.
                box.gameObject.SetActive(true);
                SendToAllReliable(NetMessages.WriteBoxDropped(boxId, box.transform.position, box.transform.eulerAngles));
                Plugin.Log.LogInfo($"[Net] Released box {boxId} after peer {peerId} disconnected.");
            }
        }

        private static void OnServerReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            NetMessages.Dispatch(peer, reader);
        }

        // Sends each client their migration role: who to reconnect to if the host drops.
        // The first connected client (index 0) becomes the next host.
        private static void BroadcastMigrationPlan()
        {
            if (_clients.Count == 0) return;

            string nextHostIp = _clients[0].Address.ToString().Split(':')[0];

            for (int i = 0; i < _clients.Count; i++)
            {
                bool isNext = i == 0;
                var  w      = NetMessages.WriteMigrationPlan(nextHostIp, isNext);
                _clients[i].Send(w, DeliveryMethod.ReliableOrdered);
            }
        }

        // ── Client ───────────────────────────────────────────────────────────────

        public static void Join(string ip, int port = DefaultPort)
        {
            if (IsRunning) Stop();

            var listener = new EventBasedNetListener();
            _client = new NetManager(listener) { AutoRecycle = true };

            listener.PeerConnectedEvent    += OnClientPeerConnected;
            listener.PeerDisconnectedEvent += OnClientPeerDisconnected;
            listener.NetworkReceiveEvent   += (peer, reader, channel, method) => NetMessages.Dispatch(peer, reader);

            _client.Start();
            _client.Connect(ip, port, ConnectionKey);

            LastPort   = port;
            LastHostIp = ip;
            IsRunning  = true;
            IsHost     = false;
            Plugin.Log.LogInfo($"[Net] Connecting to {ip}:{port}…");
        }

        private static void OnClientPeerConnected(NetPeer peer)
        {
            _hostPeer = peer;
            RemotePlayerManager.HostPeerId = peer.Id;
            RemotePlayerManager.AddPlayer(peer);
            Plugin.Log.LogInfo("[Net] Connected to host.");
            OnConnectedToHost?.Invoke();
            ClientJoinFlow.OnConnected();
        }

        private static void OnClientPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            _hostPeer = null;
            RemotePlayerManager.RemovePlayer(peer);
            Plugin.Log.LogInfo($"[Net] Disconnected from host ({info.Reason}).");
            OnDisconnectedFromHost?.Invoke();

            // Host migration takes priority over plain reconnect.
            if (_isMigrationTarget)
            {
                Plugin.Log.LogInfo("[Migration] I am the migration target — starting host.");
                _migrationReconnectTimer = 0.5f;
            }
            else if (!string.IsNullOrEmpty(_migrationTargetIp))
            {
                Plugin.Log.LogInfo($"[Migration] Reconnecting to new host at {_migrationTargetIp}…");
                _migrationReconnectTimer = 2f;
            }
            else if (!string.IsNullOrEmpty(LastHostIp))
            {
                // Plain drop — auto-retry a few times before giving up.
                _reconnectAttempts = 0;
                _reconnectTimer    = 5f;
                Plugin.Log.LogInfo("[Net] Disconnected unexpectedly — will retry in 5 s.");
            }
        }

        // Called by NetMessages when a MigrationPlan packet arrives.
        public static void StoreMigrationPlan(string nextHostIp, bool iAmNextHost)
        {
            _migrationTargetIp = nextHostIp;
            _isMigrationTarget = iAmNextHost;
            Plugin.Log.LogInfo($"[Migration] Plan updated: nextHost={nextHostIp}, iAmNext={iAmNextHost}");
        }

        // ── Send helpers ─────────────────────────────────────────────────────────

        public static void SendToAllReliable(NetDataWriter writer)
        {
            if (!IsHost) return;
            foreach (var peer in _clients)
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        public static void SendToAllUnreliable(NetDataWriter writer)
        {
            if (!IsHost) return;
            foreach (var peer in _clients)
                peer.Send(writer, DeliveryMethod.Unreliable);
        }

        public static void SendToAllUnreliableExcept(NetDataWriter writer, NetPeer except)
        {
            if (!IsHost) return;
            foreach (var peer in _clients)
                if (peer != except) peer.Send(writer, DeliveryMethod.Unreliable);
        }

        public static void SendToAllReliableExcept(NetDataWriter writer, NetPeer except)
        {
            if (!IsHost) return;
            foreach (var peer in _clients)
                if (peer != except) peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        public static void SendToSingleReliable(NetPeer peer, NetDataWriter writer)
        {
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        public static void SendToHostReliable(NetDataWriter writer)
        {
            _hostPeer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        public static void SendToHostUnreliable(NetDataWriter writer)
        {
            _hostPeer?.Send(writer, DeliveryMethod.Unreliable);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        // Read-only flag the UI uses to show a "Reconnecting…" status.
        public static bool IsReconnecting => _reconnectTimer >= 0f;
        public static int  ReconnectAttemptsLeft => MaxReconnectAttempts - _reconnectAttempts;

        public static void Poll()
        {
            _server?.PollEvents();
            _client?.PollEvents();

            DesyncDetector.Tick();

            if (_migrationReconnectTimer >= 0f)
            {
                _migrationReconnectTimer -= Time.deltaTime;
                if (_migrationReconnectTimer <= 0f)
                {
                    _migrationReconnectTimer = -1f;
                    ExecuteMigration();
                }
            }

            if (_reconnectTimer >= 0f)
            {
                _reconnectTimer -= Time.deltaTime;
                if (_reconnectTimer <= 0f)
                {
                    _reconnectTimer = -1f;
                    if (_reconnectAttempts < MaxReconnectAttempts)
                    {
                        _reconnectAttempts++;
                        Plugin.Log.LogInfo($"[Net] Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} to {LastHostIp}:{LastPort}…");
                        Join(LastHostIp, LastPort);
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[Net] Reconnect failed after max attempts — giving up.");
                        _reconnectAttempts = 0;
                    }
                }
            }
        }

        private static void ExecuteMigration()
        {
            if (_isMigrationTarget)
            {
                Plugin.Log.LogInfo("[Migration] Starting as new host.");
                _isMigrationTarget = false;
                _migrationTargetIp = null;
                StartHost(LastPort);
            }
            else
            {
                string ip   = _migrationTargetIp;
                int    port = LastPort;
                _migrationTargetIp = null;
                Plugin.Log.LogInfo($"[Migration] Joining new host at {ip}:{port}.");
                Join(ip, port);
            }
        }

        // Manual reconnect — used by the UI's "Reconnect" button after auto-retry gives up.
        public static void Reconnect()
        {
            if (string.IsNullOrEmpty(LastHostIp)) return;
            _reconnectAttempts = 0;
            Join(LastHostIp, LastPort);
        }

        public static void Stop()
        {
            _migrationReconnectTimer = -1f;
            _migrationTargetIp       = null;
            _isMigrationTarget       = false;
            _reconnectTimer          = -1f;
            _reconnectAttempts       = 0;

            bool wasHost = IsHost;
            _server?.Stop();
            _client?.Stop();
            _server   = null;
            _client   = null;
            _hostPeer = null;
            _clients.Clear();
            IsRunning = false;
            IsHost    = false;
            RemotePlayerManager.RemoveAll();
            NpcNetworkManager.Clear();
            NetMessages.ClearBoxLocks();
            NetMessages.ClearPalletLocks();
            NetMessages.ClearTrayLocks();
            LicenseTracker.Clear();
            GrowthTracker.Clear();
            DesyncDetector.Reset();
            if (!wasHost) SaveDataSync.Cleanup();
            UPnPHelper.Release();
            Plugin.Log.LogInfo("[Net] Stopped.");
        }
    }
}

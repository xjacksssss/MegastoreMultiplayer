using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MegastoreMultiplayer.Network;
using UnityEngine;

namespace MegastoreMultiplayer.UI
{
    // Always shows a status button in the top-right corner.
    // Clicking it (or pressing F8) opens/closes the connection dialog.
    // Attach to a DontDestroyOnLoad GameObject in Plugin.Awake().
    public class MultiplayerUI : MonoBehaviour
    {
        private bool   _show;
        private string _hostPort  = "7777";
        private string _joinIp    = "";
        private string _joinPort  = "7777";
        private string _playerName = "Player";
        private string _status    = "";

        private Rect _windowRect = new Rect(Screen.width - 320f, 40f, 300f, 10f);

        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialised;

        private static string _cachedLanIp;

        // ReSharper disable once UnusedMember.Local
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                ToggleUI();

            if (_show && !IsOnMainMenu && UnityEngine.Time.timeScale > 0f)
            {
                _show = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
        }

        private void ToggleUI()
        {
            _show = !_show;
            if (_show)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }

        private static bool IsOnMainMenu => SingletonBehaviour<StockManager>.Instance == null;

        private bool ShouldShowButton()
        {
            if (_show) return true;
            if (IsOnMainMenu) return true;
            if (UnityEngine.Time.timeScale == 0f) return true;
            return false;
        }

        private string StatusLabel()
        {
            if (!MultiplayerManager.IsRunning)  return "Multiplayer";
            if (MultiplayerManager.IsHost)       return $"Hosting  ({MultiplayerManager.Clients.Count} connected)";
            if (StateSnapshot.HasPending)        return "Multiplayer  (loading…)";
            return "Multiplayer  (connected)";
        }

        // ReSharper disable once UnusedMember.Local
        private void OnGUI()
        {
            if (!_stylesInitialised)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    normal    = { textColor = Color.cyan },
                    fontStyle = FontStyle.Bold,
                    fontSize  = 12,
                };
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 12,
                    fontStyle = FontStyle.Bold,
                };
                _stylesInitialised = true;
            }

            if (!ShouldShowButton()) return;

            float btnW = 200f, btnH = 28f;
            var btnRect = new Rect(Screen.width - btnW - 8f, 8f, btnW, btnH);
            if (GUI.Button(btnRect, StatusLabel(), _buttonStyle))
                ToggleUI();

            if (!_show) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            _windowRect = GUILayout.Window(9001, _windowRect, DrawWindow, "Megastore Multiplayer");
        }

        private void DrawWindow(int _)
        {
            GUILayout.Space(4);

            // ── Player name ───────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(44));
            var newName = GUILayout.TextField(_playerName, 24);
            if (newName != _playerName)
            {
                _playerName = newName;
                if (MultiplayerManager.IsRunning)
                    BroadcastMyName();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── Connected state ───────────────────────────────────────────────
            if (MultiplayerManager.IsRunning)
            {
                if (MultiplayerManager.IsHost)
                {
                    GUILayout.Label($"● Hosting on port {_hostPort}  —  {MultiplayerManager.Clients.Count} client(s)", _headerStyle);
                    GUILayout.Space(2);
                    GUILayout.Label($"Your LAN IP:  {GetLanIp()}", GUI.skin.label);
                    GUILayout.Label($"Friends connect to:  {GetLanIp()}:{_hostPort}", GUI.skin.label);
                }
                else if (StateSnapshot.HasPending)
                {
                    GUILayout.Label("● Connected — loading game...", _headerStyle);
                }
                else if (MultiplayerManager.IsReconnecting)
                {
                    GUILayout.Label($"● Reconnecting… ({MultiplayerManager.ReconnectAttemptsLeft} attempt(s) left)", _headerStyle);
                }
                else
                {
                    GUILayout.Label($"● Connected to {_joinIp}:{_joinPort}", _headerStyle);
                }

                // Desync warning + resync
                if (DesyncDetector.DesyncDetected)
                {
                    GUILayout.Space(4);
                    var warnStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow } };
                    GUILayout.Label("⚠ State desync detected!", warnStyle);
                    if (MultiplayerManager.IsClient && GUILayout.Button("Resync"))
                    {
                        DesyncDetector.RequestResync();
                        _status = "Resync requested…";
                    }
                }

                GUILayout.Space(6);
                if (GUILayout.Button("Disconnect"))
                {
                    MultiplayerManager.Stop();
                    _status = "Disconnected.";
                }

                // Manual reconnect when auto-retry gave up
                if (MultiplayerManager.IsClient && !MultiplayerManager.IsReconnecting)
                {
                    if (GUILayout.Button("Reconnect"))
                    {
                        MultiplayerManager.Reconnect();
                        _status = $"Reconnecting to {_joinIp}:{_joinPort}…";
                    }
                }
            }
            else
            {
                GUILayout.Label("● Offline", _headerStyle);
                GUILayout.Space(8);

                if (IsOnMainMenu)
                {
                    // ── Join (main menu only) ─────────────────────────────────
                    GUILayout.Label("── JOIN ──────────────────────");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("IP:", GUILayout.Width(36));
                    _joinIp = GUILayout.TextField(_joinIp);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Port:", GUILayout.Width(36));
                    _joinPort = GUILayout.TextField(_joinPort, GUILayout.Width(60));
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Join"))
                    {
                        if (int.TryParse(_joinPort, out int port))
                        {
                            MultiplayerManager.Join(_joinIp, port);
                            MultiplayerManager.OnConnectedToHost += OnJoinConnected;
                            _status = $"Connecting to {_joinIp}:{port}…";
                        }
                        else _status = "Invalid port.";
                    }
                }
                else
                {
                    // ── Host (pause screen only) ──────────────────────────────
                    GUILayout.Label("── HOST ──────────────────────");
                    GUILayout.Label($"Your LAN IP:  {GetLanIp()}");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Port:", GUILayout.Width(36));
                    _hostPort = GUILayout.TextField(_hostPort, GUILayout.Width(60));
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Start Host"))
                    {
                        if (int.TryParse(_hostPort, out int port))
                        {
                            MultiplayerManager.StartHost(port);
                            _status = $"Hosting on :{port}  —  share {GetLanIp()}:{port} with friends.";
                        }
                        else _status = "Invalid port.";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(4);
                GUILayout.Label(_status);
            }

            GUILayout.Space(4);
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void OnJoinConnected()
        {
            MultiplayerManager.OnConnectedToHost -= OnJoinConnected;
            BroadcastMyName();
        }

        private void BroadcastMyName()
        {
            var name = string.IsNullOrWhiteSpace(_playerName) ? "Player" : _playerName.Trim();
            var w    = NetMessages.WritePlayerName(name);
            if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
            else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
        }

        private static string GetLanIp()
        {
            if (_cachedLanIp != null) return _cachedLanIp;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = addr.Address.ToString();
                        if (ip.StartsWith("10.") || ip.StartsWith("192.168.") || ip.StartsWith("172."))
                        {
                            _cachedLanIp = ip;
                            return _cachedLanIp;
                        }
                    }
                }
            }
            catch { }
            _cachedLanIp = "unknown";
            return _cachedLanIp;
        }
    }
}

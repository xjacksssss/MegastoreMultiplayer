using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MegastoreMultiplayer.Network;
using MegastoreMultiplayer.UI;
using UnityEngine;

namespace MegastoreMultiplayer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal static Plugin Instance;
        private Harmony _harmony = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo("Megastore Multiplayer loading...");

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            // Probe uncertain method names — safe: logs a warning instead of crashing if wrong.
            TryPatchBoxOpen();
            TryPatchCustomerComplain();

            UpdateChecker.CheckAsync();

            // Attach the connection UI to a persistent GameObject
            var uiGo = new GameObject("MegastoreMultiplayer_UI");
            DontDestroyOnLoad(uiGo);
            uiGo.AddComponent<MultiplayerUI>();

            Log.LogInfo("Megastore Multiplayer loaded. Press F8 in-game to open the multiplayer menu.");
        }

        // Unity calls Update/OnDestroy via reflection — the analyser doesn't see these as call sites.
        // ReSharper disable once UnusedMember.Local
        private void Update()
        {
            MultiplayerManager.Poll();
            NpcNetworkManager.Tick();
            NpcNetworkManager.ClientTick(Time.deltaTime);
            RemotePlayerManager.Tick(Time.deltaTime);
        }

        private void TryPatchCustomerComplain()
        {
            var postfix = new HarmonyMethod(typeof(Customer_Complain_Patch).GetMethod(nameof(Customer_Complain_Patch.Postfix)));
            foreach (var name in new[] { "Complain", "StartComplain", "StartComplaining", "OnComplain" })
            {
                var m = HarmonyLib.AccessTools.Method(typeof(Customer), name);
                if (m == null) continue;
                _harmony.Patch(m, postfix: postfix);
                Log.LogInfo($"[MP] CustomerComplain patch applied to Customer.{name}.");
                return;
            }
            Log.LogWarning("[MP] CustomerComplain: no matching method found. Verify via dnSpy and add to TryPatchCustomerComplain.");
        }

        private void TryPatchBoxOpen()
        {
            var postfix = new HarmonyMethod(typeof(BoxManager_Open_Patch).GetMethod(nameof(BoxManager_Open_Patch.Postfix)));

            // Try BoxManager first (method that accepts a Box parameter), then Box itself.
            foreach (var (type, names) in new[]
            {
                (typeof(BoxManager), new[] { "OpenBox", "Open", "CutBox", "UnpackBox" }),
                (typeof(Box),        new[] { "Open", "OpenBox", "SetOpened" }),
            })
            {
                foreach (var name in names)
                {
                    var m = HarmonyLib.AccessTools.Method(type, name);
                    if (m == null) continue;
                    _harmony.Patch(m, postfix: postfix);
                    Log.LogInfo($"[MP] BoxOpen patch applied to {type.Name}.{name}.");
                    return;
                }
            }

            Log.LogWarning("[MP] BoxOpen: no matching method found. Verify the name via dnSpy and add it to TryPatchBoxOpen.");
        }

        private void OnDestroy()
        {
            MultiplayerManager.Stop();
            _harmony?.UnpatchSelf();
        }
    }
}
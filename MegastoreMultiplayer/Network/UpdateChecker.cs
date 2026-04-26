using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Checks the GitHub releases API once per launch and, if a newer version exists,
    // offers to download it.  The DLL cannot be replaced while loaded, so a small
    // batch script is written that swaps the files after the game process exits.
    public static class UpdateChecker
    {
        public enum CheckState
        {
            Idle,
            Checking,
            UpdateAvailable,
            UpToDate,
            Failed,
            Downloading,
            ReadyToApply,
            Dismissed,
        }

        public static CheckState State          { get; private set; } = CheckState.Idle;
        public static string     LatestVersion  { get; private set; }
        public static string     FailReason     { get; private set; }

        private static volatile string _downloadUrl;
        private static string          _pendingDllPath;

        private const string ApiUrl =
            "https://api.github.com/repos/xjacksssss/MegastoreMultiplayer/releases/latest";

        // ── Public API ────────────────────────────────────────────────────────────

        public static void CheckAsync()
        {
            if (State != CheckState.Idle) return;
            State = CheckState.Checking;
            Task.Run((Action)DoCheck);
        }

        public static void StartDownload()
        {
            if (State != CheckState.UpdateAvailable || _downloadUrl == null) return;
            State = CheckState.Downloading;
            Task.Run((Action)DoDownload);
        }

        public static void Dismiss() => State = CheckState.Dismissed;

        public static void RestartNow()
        {
            if (State == CheckState.ReadyToApply)
                Application.Quit();
        }

        // ── Check ─────────────────────────────────────────────────────────────────

        private static void DoCheck()
        {
            try
            {
                // GitHub API requires TLS 1.2 — Unity/Mono defaults to TLS 1.0.
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                request.UserAgent = $"MegastoreMultiplayer/{PluginInfo.PLUGIN_VERSION}";
                request.Timeout   = 10_000;

                string json;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader   = new StreamReader(response.GetResponseStream()))
                    json = reader.ReadToEnd();

                // "tag_name": "v0.2.0"  →  LatestVersion = "0.2.0"
                var tagMatch = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([\\d.]+)\"");
                if (!tagMatch.Success)
                {
                    Fail("Could not parse tag_name from release JSON.");
                    return;
                }

                LatestVersion = tagMatch.Groups[1].Value;

                if (new Version(LatestVersion) > new Version(PluginInfo.PLUGIN_VERSION))
                {
                    // Find the MegastoreMultiplayer.dll asset URL.
                    var urlMatch = Regex.Match(json,
                        "\"browser_download_url\"\\s*:\\s*\"(https://[^\"]*MegastoreMultiplayer[^\"]*\\.dll)\"");
                    _downloadUrl = urlMatch.Success ? urlMatch.Groups[1].Value : null;

                    State = CheckState.UpdateAvailable;
                    Plugin.Log.LogInfo(
                        $"[Updater] v{LatestVersion} available (installed: v{PluginInfo.PLUGIN_VERSION}).");
                }
                else
                {
                    State = CheckState.UpToDate;
                    Plugin.Log.LogInfo($"[Updater] Up to date (v{PluginInfo.PLUGIN_VERSION}).");
                }
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        // ── Download & apply ──────────────────────────────────────────────────────

        private static void DoDownload()
        {
            try
            {
                if (_downloadUrl == null)
                    throw new Exception(
                        "No MegastoreMultiplayer.dll asset found in the latest release. " +
                        "Download manually from the releases page.");

                var pluginDir  = Path.GetDirectoryName(typeof(UpdateChecker).Assembly.Location);
                var dllPath    = Path.Combine(pluginDir, "MegastoreMultiplayer.dll");
                _pendingDllPath = Path.Combine(pluginDir, "MegastoreMultiplayer.dll.new");
                var tmpPath    = _pendingDllPath + ".downloading";
                var batPath    = Path.Combine(pluginDir, "apply_mm_update.bat");

                // Download to a .downloading temp so an interrupted download never
                // leaves a partial .new file that the batch script would apply.
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent",
                        $"MegastoreMultiplayer/{PluginInfo.PLUGIN_VERSION}");
                    client.DownloadFile(_downloadUrl, tmpPath);
                }

                // Promote to .new only once fully downloaded.
                if (File.Exists(_pendingDllPath)) File.Delete(_pendingDllPath);
                File.Move(tmpPath, _pendingDllPath);

                // Batch script: waits for the game process to exit (5 s margin),
                // swaps the DLL, then deletes itself.
                File.WriteAllText(batPath,
                    "@echo off\r\n" +
                    "timeout /t 5 /nobreak > nul\r\n" +
                    $"move /y \"{_pendingDllPath}\" \"{dllPath}\"\r\n" +
                    "del \"%~0\"\r\n");

                // Launch hidden — it will wait 5 s after the game closes.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName       = batPath,
                    CreateNoWindow = true,
                    WindowStyle    = System.Diagnostics.ProcessWindowStyle.Hidden,
                });

                State = CheckState.ReadyToApply;
                Plugin.Log.LogInfo($"[Updater] v{LatestVersion} ready — restart to apply.");
            }
            catch (Exception ex)
            {
                // Clean up partial files so a retry is possible.
                TryDelete(_pendingDllPath + ".downloading");
                Fail(ex.Message);
            }
        }

        private static void Fail(string reason)
        {
            FailReason = reason;
            State      = CheckState.Failed;
            Plugin.Log.LogWarning($"[Updater] {reason}");
        }

        private static void TryDelete(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}

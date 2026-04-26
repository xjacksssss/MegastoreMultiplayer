using HarmonyLib;
using MegastoreMultiplayer;
using MegastoreMultiplayer.UI;
using UnityEngine;
using UnityEngine.UI;

// Injects a native "Multiplayer" button into the main menu between Continue and Load.
// The button is cloned from Continue so it inherits the same style, font, and layout.
// Clicking it opens the same F8 multiplayer panel.

[HarmonyPatch(typeof(StartWindow), nameof(StartWindow.Initialize))]
public static class StartWindow_InjectMultiplayerButton_Patch
{
    static void Postfix(StartWindow __instance)
    {
        // Search the whole scene — buttons may not be children of StartWindow.
        var all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Plugin.Log.LogInfo($"[MainMenu] StartWindow.Initialize fired. Found {all.Length} Button(s) in scene:");
        foreach (var b in all)
            Plugin.Log.LogInfo($"[MainMenu]   GO='{b.gameObject.name}'  text='{GetText(b)}'");

        // Guard against double-injection.
        if (System.Array.Exists(all, b => b.gameObject.name == "MultiplayerButton"))
            return;

        Button continueBtn = null;
        Button loadBtn     = null;

        foreach (var btn in all)
        {
            var goName = btn.gameObject.name.ToLowerInvariant();
            var text   = GetText(btn).ToLowerInvariant();

            if (continueBtn == null && (text.Contains("continue") || goName.Contains("continue")))
                continueBtn = btn;
            if (loadBtn == null && (text.Contains("load") || goName.Contains("load")))
                loadBtn = btn;
        }

        if (continueBtn == null)
        {
            Plugin.Log.LogWarning("[MainMenu] Could not find Continue button — check log above for available buttons.");
            return;
        }

        Plugin.Log.LogInfo($"[MainMenu] Injecting between '{continueBtn.gameObject.name}' and '{loadBtn?.gameObject.name}'.");

        var go  = Object.Instantiate(continueBtn.gameObject, continueBtn.transform.parent);
        go.name = "MultiplayerButton";
        SetText(go, "Multiplayer");

        int idx = loadBtn != null
            ? loadBtn.transform.GetSiblingIndex()
            : continueBtn.transform.GetSiblingIndex() + 1;
        go.transform.SetSiblingIndex(idx);

        var btn2 = go.GetComponent<Button>();
        btn2.onClick.RemoveAllListeners();
        btn2.onClick.AddListener(() => MultiplayerUI.Instance?.ToggleUI());

        Plugin.Log.LogInfo("[MainMenu] Multiplayer button injected.");
    }

    // ── Helpers — handle both legacy Text and TextMeshProUGUI ────────────────

    private static string GetText(Button btn)
    {
        var legacy = btn.GetComponentInChildren<Text>(true);
        if (legacy != null) return legacy.text;

        foreach (var c in btn.GetComponentsInChildren<Component>(true))
            if (c.GetType().Name == "TextMeshProUGUI")
                return c.GetType().GetProperty("text")?.GetValue(c) as string ?? "";

        return "";
    }

    private static void SetText(GameObject go, string value)
    {
        var legacy = go.GetComponentInChildren<Text>(true);
        if (legacy != null) { legacy.text = value; return; }

        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c.GetType().Name == "TextMeshProUGUI")
                { c.GetType().GetProperty("text")?.SetValue(c, value); return; }
    }
}

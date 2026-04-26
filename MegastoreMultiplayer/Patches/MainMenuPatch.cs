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
        // Guard against double-injection on scene reloads.
        if (__instance.GetComponentsInChildren<Button>(true) is var all &&
            System.Array.Exists(all, b => GetText(b) == "Multiplayer"))
            return;

        Button continueBtn = null;
        Button loadBtn     = null;

        foreach (var btn in all)
        {
            var t = GetText(btn);
            if (t == "Continue") continueBtn = btn;
            else if (t == "Load") loadBtn    = btn;
        }

        if (continueBtn == null)
        {
            Plugin.Log.LogWarning("[MainMenu] Could not find Continue button — multiplayer button not injected.");
            return;
        }

        // Clone Continue so we inherit all visual styling automatically.
        var go  = Object.Instantiate(continueBtn.gameObject, continueBtn.transform.parent);
        go.name = "MultiplayerButton";
        SetText(go, "Multiplayer");

        // Position: directly after Continue (before Load if it exists).
        int idx = loadBtn != null
            ? loadBtn.transform.GetSiblingIndex()
            : continueBtn.transform.GetSiblingIndex() + 1;
        go.transform.SetSiblingIndex(idx);

        // Wire up click — opens the F8 multiplayer panel.
        var btn2 = go.GetComponent<Button>();
        btn2.onClick.RemoveAllListeners();
        btn2.onClick.AddListener(() => MultiplayerUI.Instance?.ToggleUI());
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

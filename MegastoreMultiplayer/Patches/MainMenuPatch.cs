using HarmonyLib;
using MegastoreMultiplayer;
using MegastoreMultiplayer.UI;
using UnityEngine;
using UnityEngine.UI;

// LoadGameWindow is the main menu — it has three menu-level buttons:
//   continueMenuButton, loadGameMenuButton, newGameMenuButton  (SerializeField).
// We inject a Multiplayer button between Continue and Load Game.

[HarmonyPatch(typeof(LoadGameWindow), nameof(LoadGameWindow.InitializeWindow))]
public static class LoadGameWindow_InjectMultiplayerButton_Patch
{
    static void Postfix(LoadGameWindow __instance)
    {
        var t = HarmonyLib.Traverse.Create(__instance);

        var continueBtn = t.Field("continueMenuButton").GetValue<Button>();
        var loadBtn     = t.Field("loadGameMenuButton").GetValue<Button>();
        var newBtn      = t.Field("newGameMenuButton").GetValue<Button>();

        if (newBtn == null)
        {
            Plugin.Log.LogWarning("[MainMenu] newGameMenuButton not found — skipping multiplayer button injection.");
            return;
        }

        // Guard against double-injection on scene reloads.
        var parent = newBtn.transform.parent;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == "MultiplayerButton") return;

        // Clone newGameMenuButton — it is always present so it is a safe style source.
        var go  = Object.Instantiate(newBtn.gameObject, parent);
        go.name = "MultiplayerButton";
        SetText(go, "Multiplayer");

        // Position: after Continue, before Load Game (i.e. at Load Game's sibling index).
        // Falls back to just after Continue if Load isn't present.
        Button anchor = loadBtn ?? continueBtn;
        if (anchor != null)
            go.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex());
        else
            go.transform.SetSiblingIndex(newBtn.transform.GetSiblingIndex());

        // Always visible — multiplayer is available regardless of save state.
        go.SetActive(true);

        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => MultiplayerUI.Instance?.ToggleUI());

        Plugin.Log.LogInfo("[MainMenu] Multiplayer button injected.");
    }

    private static void SetText(GameObject go, string value)
    {
        // Try legacy Text first, then TextMeshProUGUI via reflection.
        var legacy = go.GetComponentInChildren<Text>(true);
        if (legacy != null) { legacy.text = value; return; }

        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c.GetType().Name == "TextMeshProUGUI")
                { c.GetType().GetProperty("text")?.SetValue(c, value); return; }
    }
}

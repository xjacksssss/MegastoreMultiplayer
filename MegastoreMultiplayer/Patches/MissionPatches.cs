using HarmonyLib;
using MegastoreMultiplayer.Network;

// Sync tutorial step completion. OnTutorialStepCompleted is private — Harmony patches by string name.
// When any player completes a step, all other players advance to the same step.

[HarmonyPatch(typeof(TutorialManager), "OnTutorialStepCompleted")]
public static class TutorialManager_OnTutorialStepCompleted_Patch
{
    static void Postfix(int tutorialStep)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteTutorialStep(tutorialStep);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

// Sync objective (post-tutorial mission) step completion.

[HarmonyPatch(typeof(ObjectiveManager), "OnObjectiveStepCompleted")]
public static class ObjectiveManager_OnObjectiveStepCompleted_Patch
{
    static void Postfix(int objectiveStep)
    {
        if (NetApply.IsApplying || !MultiplayerManager.IsRunning) return;
        var w = NetMessages.WriteObjectiveStep(objectiveStep);
        if (MultiplayerManager.IsHost)        MultiplayerManager.SendToAllReliable(w);
        else if (MultiplayerManager.IsClient) MultiplayerManager.SendToHostReliable(w);
    }
}

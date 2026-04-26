# ci-libs

Reference DLLs used by the GitHub Actions build. Not needed for local development
(the build falls back to your `GameDir` automatically).

## Game version

<!-- Update this line when you refresh the DLLs -->
**Game version:** v0.4.1  
**BepInEx version:** 5.4.23

## How to update

Run this from the repo root after the game updates, then commit:

```powershell
$game = "C:\Games\Megastore.Simulator.v0.4.1\Megastore Simulator_Data\Managed"
$bep  = "C:\Games\Megastore.Simulator.v0.4.1\BepInEx\core"

Copy-Item "$game\Assembly-CSharp.dll",
          "$game\UnityEngine.dll",
          "$game\UnityEngine.CoreModule.dll",
          "$game\UnityEngine.IMGUIModule.dll",
          "$game\UnityEngine.PhysicsModule.dll",
          "$game\UnityEngine.TextRenderingModule.dll",
          "$game\UnityEngine.InputLegacyModule.dll",
          "$game\UnityEngine.AnimationModule.dll",
          "$game\UnityEngine.AudioModule.dll",
          "$game\UnityEngine.UIModule.dll",
          "$game\DOTween.dll",
          "$bep\BepInEx.dll",
          "$bep\0Harmony.dll" -Destination ci-libs -Force

# Then commit:
# git add ci-libs/
# git commit -m "chore: update ci-libs to game vX.Y.Z"
```

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Megastore Multiplayer Mod — Claude Code Context

## Project goal

Build a multiplayer co-op mod for **Megastore Simulator** (Steam App ID: 3819640) — a singleplayer first-person store management game by Yolo Games Studio. The mod adds 2–4 player co-op via Steam lobbies. Players share a single store, split responsibilities (stocking, baking, warehouse, cashier), and see each other as characters in the world.

The game has no built-in netcode. Everything must be intercepted and synchronised via BepInEx + Harmony patches.

---

## Tech stack

| Layer | Tool | Notes |
|---|---|---|
| Mod loader | BepInEx 5.4.23 (Mono) | Tobey's preconfigured pack. Already installed and confirmed working. |
| Patching | Harmony 2 | Ships with BepInEx. Used for Prefix/Postfix/Transpiler patches. |
| Engine | Unity 6000.0.64 | Mono scripting backend (NOT IL2CPP — confirmed from log). |
| Target framework | net472 | Required for BepInEx 5 Mono compatibility. |
| Language | C# with LangVersion latest | net472 defaults to C# 7.3 so we override to latest. |
| Networking | LiteNetLib 1.3.0 | Reliable + unreliable UDP. Direct IP (no Steam required). Works on Steam, GOG, or any platform. |
| Serialisation | LiteNetLib built-in (NetDataWriter/NetDataReader) | Manual field-by-field serialisation. No external serialiser needed. |
| Decompiler | dnSpy | Used to inspect Assembly-CSharp.dll for class/method names. |

---

## Project structure

```
MegastoreMultiplayer/
├── MegastoreMultiplayer.sln
├── CLAUDE.md                        ← this file
└── MegastoreMultiplayer/
    ├── MegastoreMultiplayer.csproj
    ├── Plugin.cs                    ← BepInEx plugin entrypoint
    ├── PluginInfo.cs                ← GUID, name, version constants
    ├── Patches/                     ← Harmony patch classes (to be created)
    ├── Network/                     ← Networking layer (to be created)
    ├── Messages/                    ← Network message structs (to be created)
    └── UI/                          ← Lobby browser, HUD (to be created)
```

---

## Game installation path

The `.csproj` hard-codes the game directory as a `<GameDir>` property — update this per-machine:

```xml
<GameDir>C:\Games\Megastore.Simulator.v0.4.1</GameDir>
```

Key paths referenced in the `.csproj`:
- `BepInEx\core\BepInEx.dll`
- `BepInEx\core\0Harmony.dll`
- `Megastore Simulator_Data\Managed\UnityEngine.dll`
- `Megastore Simulator_Data\Managed\UnityEngine.CoreModule.dll`
- `Megastore Simulator_Data\Managed\Assembly-CSharp.dll`

Built DLL is auto-copied to:
```
<GameDir>\BepInEx\plugins\MegastoreMultiplayer\MegastoreMultiplayer.dll
```

This is handled by a `CopyToPlugins` MSBuild target in the `.csproj` — just run `dotnet build` and it deploys automatically.

---

## Current state

### What works
- BepInEx plugin loads and logs successfully in-game
- Harmony is initialised and `PatchAll()` is called (no patches written yet)
- Auto-deploy on build confirmed working

### Confirmed from BepInEx log
```
[Info: BepInEx] Running under Unity v6000.0.64.5464247
[Info: BepInEx] CLR runtime version: 4.0.30319.42000
[Info: BepInEx] Supports SRE: True
[Info: Megastore Multiplayer] Megastore Multiplayer loading...
[Info: Megastore Multiplayer] Megastore Multiplayer loaded successfully.
```

### What exists now
- Harmony patches for all 5 core systems (PlayerMove, Shelf, EconomyManager, OrderManager, BoxManager)
- LiteNetLib transport layer (MultiplayerManager, NetMessages, MessageType)
- Patches send real network messages when `MultiplayerManager.IsRunning`
- Receiving side logs incoming messages (apply-to-game-state is stubbed)

### What doesn't exist yet
- `Apply*` methods wired to actual game state (remote shelf changes, money, etc.)
- Remote player GameObjects and nametags
- Any UI (join-by-IP dialog, player HUD)
- State snapshot sent to joining clients

---

## Development phases

### Phase 1 — Reverse engineering (current)
Goal: map all key game classes and methods before writing any network code.

Using dnSpy to open `Assembly-CSharp.dll` and identify:
- Shelf / stock management classes
- Economy / money classes
- Order / delivery classes
- Player controller / interaction classes
- NPC / customer AI classes
- GameManager / singleton pattern

Key classes to find (names unknown until dnSpy confirms):
- `ShelfManager` or equivalent — manages shelf slots and stock levels
- `EconomyManager` or equivalent — handles money add/deduct
- `OrderManager` or equivalent — handles truck deliveries and ordering
- `PlayerController` or equivalent — movement, interaction raycast
- `InventorySystem` or equivalent — items in hand / warehouse stock

Existing mods to learn from (their DLLs reveal class names):
- **SmartRestock** — already patches ShelfManager, InventorySystem, delivery queue
- **Trainer mod** — already patches EconomyManager (AddMoney, AddXP)
- **Additional Products** — shows how product/shelf data is structured

### Phase 2 — Core networking (MVP) ← current
Goal: 2 players in the same store with shelf sync and shared money.

- ✅ LiteNetLib transport (host/client, reliable + unreliable channels)
- ✅ Patches wired — position, shelf, money, orders, box pickup all send real packets
- ⬜ Apply received packets to game state (shelf stock, money balance, box positions)
- ⬜ Full state snapshot sent to joining client
- ⬜ Remote player GameObjects with nametags
- ⬜ Join-by-IP UI (host shares their LAN/WAN IP; client enters it)

### Phase 3 — Full feature set
Goal: 4 players, all game systems synced, polished connection UX.

- NPC/customer position sync (host simulates, broadcasts deltas)
- Bakery sync (oven timer, dough orders, baked goods)
- Forklift / pallet jack sync
- Public lobby browser (LAN discovery via LiteNetLib broadcast)
- Optional: Steamworks.NET overlay for Steam users (lobby invites, rich presence)

### Phase 4 — Polish & release
Goal: resilient, maintainable, publicly released mod.

- Game update detector (warn if Assembly-CSharp.dll hash changed)
- Desync detection and recovery (periodic state hash comparison)
- Reconnect on disconnect
- Nexus Mods + Thunderstore release
- GitHub CI: build + patch smoke test on each commit

---

## Architecture decisions

### Host authority model
One player is the host (server). All state-changing actions go through the host:
- Clients send **requests** (RequestPurchase, RequestOrder, RequestPickup)
- Host **validates** and broadcasts **confirmed results** to all clients
- Clients never directly modify money, shelf stock, or order state — they only apply confirmed results from the host

### Network channels
- **Reliable**: orders, money changes, join/leave events, state snapshots
- **Unreliable**: player positions (high frequency, stale data acceptable)

### Harmony patch pattern
```csharp
[HarmonyPatch(typeof(SomeGameClass), nameof(SomeGameClass.SomeMethod))]
public static class SomeMethod_Patch
{
    // Prefix: runs BEFORE the original method. Return false to skip original.
    static bool Prefix(SomeGameClass __instance, ref SomeParam param)
    {
        // intercept here
        return true; // true = let original run, false = skip original
    }

    // Postfix: runs AFTER the original method.
    static void Postfix(SomeGameClass __instance, SomeReturnType __result)
    {
        // react to result here
    }
}
```

### Message pattern
Messages are serialised manually with `NetDataWriter` / `NetDataReader`. Each packet starts with a `MessageType` byte.

```csharp
// Sending (host example)
var w = NetMessages.WriteShelfUpdate(shelfId, (int)productType, newCount);
MultiplayerManager.SendToAllReliable(w);

// Receiving (inside NetMessages.Dispatch)
case MessageType.ShelfUpdate:
    string shelfId     = r.GetString();
    int    productType = r.GetInt();
    int    newCount    = r.GetInt();
    // apply to game state...
```

---

## Networking plan (LiteNetLib)

### Dependency (already in .csproj)
```xml
<PackageReference Include="LiteNetLib" Version="1.3.0" />
```
LiteNetLib.dll is auto-copied to the BepInEx plugins folder by the `CopyToPlugins` MSBuild target.

### Key classes
| Class | Role |
|---|---|
| `Network/MultiplayerManager.cs` | Start/stop host or client, send helpers, peer lists |
| `Network/NetMessages.cs` | Write* serialisers + Dispatch router + Apply* stubs |
| `Messages/MessageType.cs` | Byte enum of all message types |

### Connection flow
1. Host calls `MultiplayerManager.StartHost(port)` — binds UDP on port 7777
2. Host shares their IP with friends (LAN IP or port-forwarded WAN IP)
3. Client calls `MultiplayerManager.Join(ip, port)`
4. On `PeerConnectedEvent` → host sends full `StateSnapshot` to new client (TODO)
5. Client applies snapshot → begins receiving delta updates

### Channels
- `DeliveryMethod.ReliableOrdered` — shelf changes, money, orders, box events, snapshot
- `DeliveryMethod.Unreliable` — player positions (20 Hz, stale data acceptable)

---

## Known risks

| Risk | Mitigation |
|---|---|
| Game update breaks Harmony patches | Version-pin in README, hash-check on load, CI smoke test |
| NPC simulation cost on host | Sync NPC position at 500ms intervals only, no AI on clients |
| State desync between host and clients | Periodic CRC hash comparison, `/resync` command for manual recovery |
| NAT / port forwarding friction | Document port 7777 UDP requirement; add LAN-discovery fallback later |
| Two players grabbing same item simultaneously | Lock item on grab server-side, reject second grab request |

---

## Scope

### In scope (MVP)
2-player co-op, direct IP connect (no Steam required), player position sync, shelf stock sync, shared money, order sync, inventory pickup sync, host migration (basic), player nametags.

### In scope (full release)
4-player support, NPC sync, bakery sync, forklift sync, public lobby browser, Steam achievements, proximity voice, emote wheel, anti-cheat money validation.

### Out of scope
Dedicated server binary, cross-platform (Windows only initially), saved co-op sessions, more than 4 players, Steam Workshop integration.

---

## How to build and test

```bash
# Build and auto-deploy to BepInEx plugins
dotnet build

# Check mod loaded successfully
# Open: <GameDir>\BepInEx\LogOutput.log
# Look for: [Info: Megastore Multiplayer] Megastore Multiplayer loaded successfully.
```

## How to add a Harmony patch

1. Create a new file in `Patches/` e.g. `Patches/ShelfPatches.cs`
2. Add a static class decorated with `[HarmonyPatch]`
3. `PatchAll()` in `Plugin.Awake()` will pick it up automatically — no registration needed

---

## Game facts (from Steam page)

- Genre: First-person store management / tycoon
- Engine: Unity 6
- Departments: Bakery, Clothing, Electronics, Sports, Music, Toy, Grocery
- Features: Two-floor store, warehouse with 4 truck bays, forklifts, pallet jacks, NPC staff (cashiers, stockers, bakers), customer NPCs, returns desk, recycling bins, ads/promotions
- State: Early Access (released Feb 2 2026), actively updated by developer
- Player base: ~500 reviews, Very Positive

---

## Resources

- [BepInEx docs](https://docs.bepinex.dev/)
- [Harmony docs](https://harmony.pardeike.net/)
- [LiteNetLib docs / wiki](https://github.com/RevenantX/LiteNetLib/wiki)
- [Tobey's BepInEx Pack (Nexus)](https://www.nexusmods.com/megastoresimulator/mods/2)
- [Tobey's BepInEx Pack (GitHub)](https://github.com/toebeann/BepInEx.MegastoreSimulator)
- [Megastore Simulator Nexus Mods](https://www.nexusmods.com/megastoresimulator)
- dnSpy: search "dnSpy releases GitHub" for latest build
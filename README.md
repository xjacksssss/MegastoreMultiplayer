# Megastore Multiplayer

A co-op multiplayer mod for **[Megastore Simulator](https://store.steampowered.com/app/3819640/Megastore_Simulator/)** that adds 2–4 player support via direct IP connection. Players share a single store, see each other in the world, and can split responsibilities across every department.

> **Early Access mod** — built against game version **v0.4.1**. Game updates may break patches until the mod is updated.

---

## Features

### Synced systems
| System | What's synced |
|---|---|
| Players | Position, rotation, nametags, held boxes |
| Economy | Money balance, XP, level-ups |
| Shelves | Product type and stock count per shelf |
| Prices | Per-product unit prices |
| Orders | Delivery orders placed at all 5 receiving areas |
| Boxes | Pick-up, drop, throw, open state, recycling |
| Pallets | Pick-up (foot and forklift), drop, throw, trash |
| Vehicles | Hand trucks, forklifts, pallet jacks — position and load state |
| Bakery | Oven start/complete with elapsed-time accuracy, tray and dough sync |
| Chopping stand | Sit/leave, NPC orders, product handoff |
| Checkout desks | Customer queue, belt products, scanning, payment (cash and card) |
| Customers | Spawn, despawn, position (10 Hz with dead-reckoning), animations |
| Employees | Hire/fire/update, activation, position (10 Hz), work animations |
| Customer cars | Spawn, movement, despawn |
| Delivery trucks | Arrival and departure |
| Store state | Open/close sign, lights, store name, licenses, growth |
| Vending machines | Money accumulated and collected |
| Decorations | Floor and wall decoration changes |
| Ads/promotions | Offer activation and deactivation |
| Time | Day/night cycle, day-end, new-day transitions |
| Furniture | Shelf and counter repositioning |

### Networking
- **Direct IP** — no Steam requirement; works on any platform (Steam, GOG, etc.)
- **Port** `7777 UDP` (configurable)
- **Host authority** — all game-state changes are validated by the host
- **Host migration** — if the host disconnects, the next client promotes automatically
- **Auto-reconnect** — clients retry up to 3 times on unexpected disconnect
- **Desync detection** — periodic state hash comparison with `/resync` recovery
- **Late join** — full state snapshot sent to connecting clients mid-session

---

## Requirements

| Requirement | Version |
|---|---|
| Megastore Simulator | v0.4.1 (Early Access) |
| [Tobey's BepInEx Pack](https://www.nexusmods.com/megastoresimulator/mods/2) | 5.4.23 |

---

## Installation

### Option A — release download *(recommended)*
1. Install [Tobey's BepInEx Pack](https://www.nexusmods.com/megastoresimulator/mods/2) if you haven't already.
2. Download the latest release zip from the [Releases](../../releases) page.
3. Extract into your game folder. The result should be:
   ```
   <Game>\BepInEx\plugins\MegastoreMultiplayer\
       MegastoreMultiplayer.dll
       LiteNetLib.dll
   ```
4. Launch the game. Press **F8** in-game to open the multiplayer menu.

### Option B — manual build
See [Building from source](#building-from-source).

---

## How to play

### Hosting
1. Open the multiplayer menu (**F8**).
2. Click **Host** (default port 7777).
3. Share your **LAN IP** with friends on the same network, or your **WAN IP** if port-forwarding `7777 UDP`.

### Joining
1. Open the multiplayer menu (**F8**).
2. Enter the host's IP address and click **Join**.
3. The host's save file is automatically synced — no manual save copying needed.

> **Note:** The client loads the host's store. Your own save file is never modified; the mod uses a temporary slot (`Save_99.data`) that is deleted on disconnect.

---

## Known limitations

- **Game updates** — Harmony patches target specific class and method names. A game update that renames or restructures code will break affected patches until the mod is updated. The mod logs a warning if it can't find a patched method.
- **Sound events** — multiplayer sound cues (oven ding, truck horn, store open chime) rely on the game's own `AudioSource` components. If a game update restructures audio, sounds may be silent on clients.
- **4-player support** — tested primarily with 2 players. 3–4 player sessions may surface edge cases not yet covered.

---

## Building from source

### Prerequisites
- [.NET SDK 8+](https://dotnet.microsoft.com/download) (only for building; the mod targets net472)
- Megastore Simulator installed
- Tobey's BepInEx Pack installed into the game

### Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/xjacksssss/MegastoreMultiplayer.git
   cd MegastoreMultiplayer
   ```

2. Update the game path in `MegastoreMultiplayer/MegastoreMultiplayer.csproj`:
   ```xml
   <GameDir>C:\Games\Megastore.Simulator.v0.4.1</GameDir>
   ```

3. Build and auto-deploy to your BepInEx plugins folder:
   ```bash
   dotnet build
   ```

4. Launch the game and check `BepInEx\LogOutput.log` for:
   ```
   [Info: Megastore Multiplayer] Megastore Multiplayer loaded successfully.
   ```

---

## Project structure

```
MegastoreMultiplayer/
├── Messages/
│   └── MessageType.cs          — byte enum of all 76 message types
├── Network/
│   ├── MultiplayerManager.cs   — host/client lifecycle, send helpers
│   ├── NetMessages.cs          — serialisers, dispatch router, apply methods
│   ├── StateSnapshot.cs        — full-state sync for joining clients
│   ├── SaveDataSync.cs         — host save file sync to joining clients
│   ├── NpcNetworkManager.cs    — NPC proxy management and interpolation
│   ├── RemotePlayerManager.cs  — remote player avatars and nametags
│   ├── DesyncDetector.cs       — periodic hash comparison
│   └── *Registry.cs            — shelf, oven, tray, vehicle, furniture registries
├── Patches/                    — one file per game system (31 patch files)
├── UI/
│   └── MultiplayerUI.cs        — F8 overlay (host/join/status)
├── Plugin.cs                   — BepInEx entry point
└── PluginInfo.cs               — GUID, name, version constants
```

---

## Architecture

### Host authority
The host is the single source of truth. Clients send **requests** (pick up box, order stock, scan product); the host validates and broadcasts **confirmed results** to all clients. Clients never directly modify money, shelf stock, or order state — they only apply results the host confirms.

### Network channels
- **Reliable ordered** — shelf changes, money, orders, NPC spawn/despawn, state snapshot
- **Unreliable** — player positions (20 Hz), NPC positions (10 Hz with dead-reckoning)

### Contention prevention
Boxes, pallets, and trays are server-side locked on pickup. A second player attempting to grab a locked item receives a correction packet placing it back at its current position.

---

## Contributing

Pull requests are welcome. When adding sync for a new game system:

1. Identify the relevant class and method using **dnSpy** on `Assembly-CSharp.dll`.
2. Add a `Patches/YourSystemPatches.cs` file using the existing patch pattern.
3. Add a `MessageType` entry in `Messages/MessageType.cs`.
4. Add `Write*` / `Apply*` methods in `Network/NetMessages.cs`.
5. Add the system's state to `Network/StateSnapshot.cs` so late joiners are covered.

---

## License

[MIT](LICENSE) — you are free to use, modify, and redistribute this mod. You may not redistribute the game's original DLLs or decompiled source code.

---

## Resources

- [BepInEx docs](https://docs.bepinex.dev/)
- [Harmony docs](https://harmony.pardeike.net/)
- [LiteNetLib wiki](https://github.com/RevenantX/LiteNetLib/wiki)
- [Tobey's BepInEx Pack (Nexus)](https://www.nexusmods.com/megastoresimulator/mods/2)
- [Megastore Simulator on Steam](https://store.steampowered.com/app/3819640/Megastore_Simulator/)

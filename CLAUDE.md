# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MazeEscape** is a Unity multiplayer game (Unity 6, URP) where players navigate procedurally generated mazes, find a key, and escape through an exit door. Built on Unity Netcode for GameObjects with both host/client and Unity Multiplayer Services support.

## Unity Development Commands

This is a Unity project — there is no CLI build or test command. Development happens through the Unity Editor (Unity 6000.x). The Unity MCP server (`com.ivanmurzak.unity.mcp`) is configured in `.mcp.json` for editor integration.

- **Open project:** Open the folder in Unity Hub
- **Run game:** Use Play mode in the Unity Editor
- **Build:** File → Build Settings in Editor
- **Tests:** Window → General → Test Runner in Editor

## Architecture

### Core Player System (Composition Pattern)
`CorePlayerManager` (`Assets/Core/Scripts/Runtime/Components/`) is the central orchestrator. It holds references to all player subsystems:
- `CoreInputHandler` — new Input System events, only active on local owner
- `CoreMovement` — CharacterController-based movement, extends `NetworkTransform`
- `CoreStatsHandler` — `NetworkList<RuntimeStat>` for synced health/stamina, driven by `StatsConfig` ScriptableObjects
- `CoreCameraController` — third-person look, Cinemachine integration
- `CoreAnimator` — bridges movement state to animator via `NetworkAnimator`

Player capabilities are extended via **`IPlayerAddon`** plugins (auto-discovered via `GetComponents`): `ShooterAddon`, `PlatformerAddon`, `DoubleJumpAddon`, `NamePlateAddon`, `VisualsAddon`.

### Networking Authority Model
- **Server authority:** Key pickup/drop, match end, player stats writes
- **Owner authority:** Movement, input, camera
- `NetworkVariable` used for `MatchManager` (WinnerId, MatchEnded), key carrier state
- Late-joining clients receive current state automatically via `NetworkVariable`

### Shooter Subsystem (`Assets/Shooter/`)
`ModularWeapon` is composed at runtime from strategy interfaces:
- `IFiringMechanism` → `SingleShotMechanism`, `BurstMechanism`, `AutomaticMechanism`
- `IShootingBehavior` → `HitscanShooting`, `ProjectileShooting`, `ShotgunShooting`
- `AmmoHandler`, `SpreadHandler`, `WeaponStateManager` (Idle/ReadyToFire/Reloading/Obstructed)

`WeaponData` ScriptableObjects configure everything: damage, spread, clip size, effects, sounds, animation IDs.

### Maze Generation (`Assets/MazeGenerator/Scripts/MazeGenerator.cs`)
Procedural DFS algorithm:
1. Place random rooms (3×3 to 5×5 cells)
2. Carve DFS corridors avoiding rooms
3. Carve room interiors and connect each to the corridor network via a doorway

Seed-based for reproducibility. `MazeRenderer` renders the maze using prefab walls/floors.

### MazeEscape Game Mode (`Assets/Scripts/`)
- `NetworkPickupKey` — key spawns at maze bottom-right; server-authoritative pickup/drop; drops on carrier death; gold emissive only visible to carrier
- `ExitDoor` — at maze top-left; triggers `MatchManager.EndMatch(winnerId)` when key carrier enters
- `MatchManager` — `NetworkVariable<ulong> WinnerId`, `NetworkVariable<bool> MatchEnded`
- `MatchResultUI` — shows win/lose screen to all clients

### Game Events System (`Assets/Core/Scripts/Runtime/GameEvents/`)
Decoupled event architecture with typed events: `GameEvent`, `GameEvent<T>`, `Vector2Event`, `FloatEvent`, `BoolEvent`, `StatChangeEvent`, `StatDepletedEvent`, `PlayerStateEvent`. Events are wired in the Inspector; systems call `RegisterListener`/`UnregisterListener`.

### Sound System
Builder pattern via `CoreDirector.RequestAudio(soundDef).WithPosition(...).Play()`. `SoundDef` ScriptableObjects configure clips, volume, pitch, and spatial blend. Object pooling managed by `SoundSystem` singleton.

## Key Packages
- Unity Netcode for GameObjects: 2.7.0
- Unity Transport: 2.6.0
- Input System: 1.17.0
- Cinemachine: 3.1.5
- URP: 17.3.0
- Animation Rigging: 1.4.0
- Unity MCP: com.ivanmurzak.unity.mcp 0.63.3

## Working Rules

- **Before writing new code, always read the existing working example first.** Follow existing patterns exactly — do not invent new architectures.
- **All pickups must use the `ModularInteractable` system** (`Assets/Shooter/Scripts/Runtime/Components/ModularInteractable.cs`). Do not create new interaction systems.
- **Interaction effects inherit from the base effect pattern** — see `ModifyStatEffect.cs` (`Assets/Shooter/Scripts/Runtime/Components/ModifyStatEffect.cs`) as the reference.
- **Reference prefab:** `Assets/Prefabs/Pfb_Collectible.prefab` is the canonical collectible example.
- **Ask before modifying existing scripts.**
- If unsure how something works, read the code before guessing.

## Conventions
- ScriptableObjects are used for all data configuration (stats, weapons, sounds)
- Namespaces: `Blocks.Gameplay.Core` for core framework, `MazeEscape` for game mode scripts
- `IHittable.OnHit(HitInfo)` is the interface for anything that receives damage
- `IInteractable.OnInteract(interactorId)` for interactive objects (doors, pickups)
- Player state machine: `InitialSpawn → Active → Eliminated → Respawned`

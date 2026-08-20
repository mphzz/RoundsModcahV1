# Rounds Modcah

A [BepInEx](https://github.com/BepInEx/BepInEx)-compatible mod for [ROUNDS](https://store.steampowered.com/app/1557740/ROUNDS/) that adds a set of custom cards, ranging from simple stat tweaks to a fully custom sticky-explosive projectile system.

Built on top of [UnboundLib](https://github.com/Rounds-Modding/UnboundLib), [ModdingUtils](https://github.com/pdcook/ModdingUtils), and [CardChoiceSpawnUniqueCardPatch](https://github.com/Rounds-Modding/CardChoiceSpawnUniqueCardPatch).

---

## Cards

| Card | Effect |
|---|---|
| **Evasive Maneuvers** | Utility/movement card. |
| **Brute** | +250 max health. |
| **Get Em** | +20 Ammo, +15% Damage, -15% Movement Speed. Blocking teleports you to the nearest enemy. |
| **Oh Yeah;** | Attack speed tuning card (see `SetupCard` for current values). |
| **Redditor's Burden** | +150% Health, -60% Movement Speed. |
| **Dead Space** | Kills from your bullets cannot be revived — forces a permanent death instead of the normal downed/revivable state. |
| **Cat** | +1 Block charge, +50% Movement Speed. |
| **Over Powered** | +10 Bullets, +250% Attack Speed, -50% Damage. |
| **Sugar Rush Pill** | -15% Movement Speed. Blocking grants a temporary +400% Movement Speed boost that decays over 10s, on a 15s cooldown. |
| **Poop Party** | +60 Bullets, -95% Damage, brown bullets. +200% Attack Speed (with true auto-fire) while standing still. |
| **Stick Nade** | Bullets stick to whatever they hit (players, walls, or movable/destructible objects) and explode after a short fuse. See below for full details. |

---

## Stick Nade — feature breakdown

This is the most involved card in the mod, built almost entirely with Harmony patches since it required intercepting and rebuilding core bullet behavior:

- **Sticking**: intercepts `ProjectileHit.RPCA_DoHit` before it applies damage/destroys the bullet. Freezes the bullet's movement, disables its colliders and `RayCastTrail`, and parents it to whatever it hit — but **only** if the target can actually move (a player, or an object with a `NetworkPhysicsObject`). Static geometry is left unparented to avoid scale-distortion issues on angled/scaled wall segments.
- **Direct hit damage**: deals normal bullet damage immediately on a direct hit to anything `Damagable` (covers both players and world objects like Sandbox mode's shoot-to-claim cards).
- **Blocking**: respects normal block mechanics — a blocked hit defers to vanilla block handling instead of sticking.
- **Fuse & warning**: flashes red/white and beeps faster as the fuse runs down, then detonates.
- **Explosion**:
  - AOE damage + knockback to players.
  - Pushes movable/destructible physics props via `NetworkPhysicsObject.BulletPush`.
  - Line-of-sight checked — walls and non-player objects block the blast, but players never shield each other.
  - Non-interactable "background" objects (things real bullets pass through) are correctly ignored, using the same layer masks the bullet itself uses.
  - A shockwave ring visual expands from the blast center and clips against walls to show the real affected area.
- **Respawn safety**: if a player dies with a nade still stuck to them, the nade is forced to detonate immediately on respawn (via a `HealthHandler.Revive` patch) rather than riding along frozen through the respawn. A second safety net hooks `GameModeHooks.HookRoundStart` in case a new round resets players through a different path. The just-revived player is immune to this forced blast.
- **Custom audio**: uses a real, confirmed explosion sound pulled from the game's own `SoundGun.soundImpactModifierDamageToExplosionHuge`, plus a custom embedded `.wav` for the countdown beep (loaded via an embedded resource, extracted to a temp file at runtime).
- **Custom card art**: loaded from an embedded PNG resource at runtime and built into a `UnityEngine.UI.Image` GameObject matching the game's actual card art display system.

---

## Dependencies

- [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.19+
- [UnboundLib](https://github.com/Rounds-Modding/UnboundLib) — `willis81808-UnboundLib`
- [ModdingUtils](https://github.com/pdcook/ModdingUtils) — `Pykess-ModdingUtils`
- [CardChoiceSpawnUniqueCardPatch](https://github.com/Rounds-Modding/CardChoiceSpawnUniqueCardPatch) — `Pykess-CardChoiceSpawnUniqueCardPatch`
- 0Harmony (bundled with BepInEx)

## Building

This project targets `netstandard2.1` and references game/library DLLs directly from a local ROUNDS installation. Update the `HintPath` entries in `RoundsModcah.csproj` to point at your own:

- `.../ROUNDS_Data/Managed/` (for `Assembly-CSharp`, `UnityEngine.*` modules, `SonigonAudioEngine.Runtime`, `PhotonUnityNetworking`, etc.)
- Your local BepInEx `core` folder (for `0Harmony`, `BepInEx`)
- Your BepInEx `plugins` folder (for `UnboundLib`, `ModdingUtils`, `CardChoiceSpawnUniqueCardPatch`)

Two resources are embedded directly into the compiled DLL rather than shipped as loose files, so they survive mod deployment/syncing intact:

- `sticknade_beep.wav`
- `sticknade_art.png`

Both must be present in the project source directory and are referenced as `<EmbeddedResource>` items in the `.csproj`.

## Installation

1. Install BepInEx and the dependencies listed above (r2modmanPlus handles this automatically if the dependencies are declared correctly in your Thunderstore manifest).
2. Copy `RoundsModcah.dll` into your `BepInEx/plugins` folder.
3. Launch the game through your mod manager.

---

## Credits

Built with reverse-engineering help from decompiling `Assembly-CSharp.dll` and `UnboundLib.dll` in dnSpy — a lot of this mod's mechanics (sticky projectiles, forced revive detonation, layer-mask-aware explosions) required reading the game's actual compiled source rather than guessing, since no public documentation covers these systems.

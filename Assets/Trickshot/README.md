# Trickshot prototype

Press Play in any scene. If the scene has no player, `TrickshotAutoStart` builds a test range:
a tower to jump off, 14 targets, the player, the rifle and the HUD.

## Controls

| Input | Action |
| --- | --- |
| WASD / Shift / Space | Move / sprint / jump |
| Right mouse (hold) | Scope |
| Left mouse | Fire |
| R | Back to the top of the tower |
| [ and ] | Mouse sensitivity down / up |
| Esc | Free the cursor (click to resume) |

## Scoring

`points = target base x scope x spin x style`

| Part | Rule | Default |
| --- | --- | --- |
| Scope: no-scope | Highest multiplier, but shots land in a random 2 degree cone | x3.0 |
| Scope: quickscope | Fired within 0.5 s of scoping in; faster is worth more | x2.5 down to x1.5 |
| Scope: hardscope | Keeps draining the longer you stay scoped | x1.5 down to x0.1 at 3 s |
| Spin | Each 180 degrees turned in the air before the shot | +0.5 (360 = x2, 720 = x3) |
| Style: airborne | Shot fired while off the ground | +0.5 |
| Style: longshot | Hit from 60 m or more | +0.5 |
| Target base | Orange static / red moving | 100 / 150 |

Example: no-scope 720 off the tower onto a far static target = 100 x 3.0 x 3.0 x 2.0 = 1,800.

Details worth knowing:

- The scope takes 0.22 s to come up and the rifle is only pinpoint once it has, so the best
  accurate quickscope lands around x2.0.
- The scope timer only resets once the scope is fully down, so tapping right mouse does not
  turn a hardscope back into a quickscope.
- Spins are net rotation in one direction. Turning back subtracts, and a spin is used up by the shot.
- A spin stays valid for 0.25 s after landing.

## Tuning

Every number above is a public field. While playing, select these in the Hierarchy:

- `Trickshot Bootstrap` > **Score Manager**: all multipliers, quickscope window, spin step, bonuses.
  Set `Hardscope Floor` below zero if you want long hardscopes to subtract points.
- `Player` > **Player Controller**: speed, jump height, gravity, sensitivity, `Count Ground Spins`.
- `Main Camera` > **Sniper Rifle**: scope-in time, zoom, bolt time, no-scope spread.

Changes made in Play mode are lost when you stop, so copy the values you like into the
defaults at the top of each script.

## Files

| Script | Job |
| --- | --- |
| `ScoreManager` | The scoring rules and running totals |
| `PlayerController` | Movement, mouse look, spin tracking |
| `SniperRifle` | Scoping, scope timer, firing, hit detection |
| `Target` | Shootable object, optional movement, respawn |
| `TrickshotHUD` | Score, live multipliers, scope overlay, shot popups |
| `TSInput` | All key and mouse bindings in one place |
| `TrickshotBootstrap` | Builds the test range in code |
| `TrickshotAutoStart` | Spawns the range on Play; delete when you have your own level |

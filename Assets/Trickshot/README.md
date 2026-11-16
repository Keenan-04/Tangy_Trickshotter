# Tangy Trickshotter

Press Play in any scene. If the scene has no player, `TrickshotAutoStart` builds everything in code
and opens the main menu.

## Main menu

Modes are listed down the left, your character stands on the right, and there are three tabs.

| Tab | What it does |
| --- | --- |
| Play | Pick Training, Team Deathmatch or Elimination, read its rules, press PLAY |
| Inventory | TEMPLATE. Lists what you own; click to equip a rifle finish or an outfit |
| Store | TEMPLATE. Buy finishes and outfits with credits |

Use the mouse, or 1 / 2 / 3 to select a mode and Enter to play. Press M in any mode to come back.

Items are placeholder colours defined in `Profile.Catalog`. The equipped outfit and finish show on
the menu character, and the finish also colours the first-person rifle. You start with 500 credits,
earn 1 per 100 points of a training round, and 50 for winning a match. Credits, purchases and the
loadout are saved in PlayerPrefs.

The character is a placeholder figure built from boxes (`CharacterModel`). Replace it with a real model.

## Main game modes (templates)

Team Deathmatch and Elimination are playable stand-ins that run on the training range. The enemy
team is red dummies that strafe but do not shoot back, so the rules, score bar, clock and
win/lose flow can be built on before there are real opponents or maps.

| Mode | Rules as shipped |
| --- | --- |
| Team Deathmatch | First to 30 kills, 5 minute limit, 5 enemies up at once, enemies respawn after 2 s |
| Elimination | First to 4 rounds, 4 enemies per round, no respawns, 90 s rounds; running out of time loses the round |

Both extend `MatchMode`. Put the rules in `OnBegin`, `Tick` and `OnEnemyKilled`, and replace
`SpawnEnemy` when there are real players or bots. Because the enemies cannot score, Team Deathmatch
cannot be lost yet.

## Sounds

The rifle shot, bolt, scope-in, hitmarker tick, kill tone, headshot ring and menu click are
placeholder sounds synthesised in `Sfx.cs`, so the project has no audio files. To use real
recordings, drop clips into the **Sounds** fields on `Main Camera` > **Sniper Rifle**.

# Training: the sniper aim trainer

The range is a flat firing lane with distance lines at 15, 30 and 45 m, a back wall, a box to jump
off and a settings panel. You start in warm-up with targets up and nothing recorded. Press 1, 2 or 3
to start a 60 second round.

## Modes

| Key | Mode | What it trains |
| --- | --- | --- |
| 1 | Flick | Three targets at 15 to 45 m, standing still. A new one appears for every kill. |
| 2 | Strafe | Two targets at 18 to 38 m with strafing switched on. |
| 3 | Quickscope | One target at a time at 12 to 30 m. A hardscoped hit scores nothing. |

The results screen shows score, kills per minute, headshot rate and accuracy. Best scores are
saved between sessions, separately for each combination of mode and panel settings.

## Settings panel

The board stands to the front left of the firing point. Look at a control and click it. The rifle
does not fire while you are aiming at the board, and a white dot shows where you are pointing.

| Control | What it does | Range |
| --- | --- | --- |
| Strafing | Switches strafing on or off for every dummy, in any mode | on / off |
| Strafe speed | How fast they strafe | 1 to 8 m/s |
| Head size | Head diameter, hitbox included | half to three times the normal 25 cm |
| Round length | How long the next round is scored for | 15 to 180 s |
| START | Starts a round of the current mode with these settings | |

Hold the mouse button and move along a slider to drag it. Strafing, speed and head size change the
dummies that are already up. Pressing 1, 2 or 3 sets strafing back to that mode's default.

## Controls

| Input | Action |
| --- | --- |
| 1 / 2 / 3 | Start (or restart) a round |
| M | Back to the main menu |
| WASD / Shift / Space | Move / sprint / jump |
| Right mouse (hold) | Scope |
| Shift (while scoped) | Hold breath to steady the scope |
| Left mouse | Fire |
| R | Back to the firing point |
| [ and ] | Mouse sensitivity down / up |
| Esc | Free the cursor (click to resume) |

## Targets

Each dummy is a 1.8 m person made of three hit zones:

| Zone | Size | Result |
| --- | --- | --- |
| Head | 25 cm sphere, centred 1.64 m up | Kill, headshot multiplier |
| Torso | 55 x 60 cm | Kill |
| Legs | 42 x 90 cm | Hitmarker, second shot kills |

A white hitmarker is a hit, a red one is a kill.

## How the scope behaves

- The rifle takes 0.38 s to come up. The black scope picture only appears for the last part of that.
- A shot is pinpoint from the moment the scope is fully up. Any shot before that, and any no-scope,
  lands in a random 5 degree cone. There is no crosshair at the hip.
- The scope drifts in a slow figure of eight. Holding Shift steadies it for up to 4 s. Run out of
  breath and it sways more than normal until you recover.
- A shot throws the scope up and it settles back by itself while the bolt cycles (1 s between shots).
- You move at 40% speed while scoped, and mouse speed follows the zoom.
- The scope timer only resets once the scope is fully down, so tapping right mouse does not
  turn a hardscope back into a quickscope.

## Scoring

`points = base x head x scope x spin x style`

| Part | Rule | Default |
| --- | --- | --- |
| Base | Kill / leg hit that does not kill | 100 / 25 |
| Head | Headshot | x1.5 |
| Scope: no-scope | Highest multiplier, but the shot is in the hip-fire cone | x3.0 |
| Scope: quickscope | Fired within 0.65 s of starting to scope; faster is worth more | x2.5 down to x1.5 |
| Scope: hardscope | Keeps draining the longer you stay scoped | x1.5 down to x0.1 at 3 s |
| Spin | Each 180 degrees turned in the air before the shot | +0.5 (360 = x2, 720 = x3) |
| Style: airborne | Shot fired while off the ground | +0.5 |
| Style: longshot | Hit from 40 m or more | +0.5 |

Because the scope takes 0.38 s to come up, the best accurate quickscope lands around x1.9.
A quickscope headshot is 100 x 1.5 x 1.9 = 285.

Spins are net rotation in one direction, are used up by the shot, and stay valid for 0.25 s after landing.

## Tuning

Every number above is a public field. While playing, select these in the Hierarchy:

- `Trickshot Bootstrap` > **Aim Trainer**: round length, spawn arc, distances per mode, strafe speed.
- `Config Panel` > **Config Panel**: the range of each slider.
- `Trickshot Bootstrap` > **Score Manager**: all multipliers, quickscope window, spin step, bonuses.
- `Trickshot Bootstrap` > **Trickshot HUD**: `Hip Crosshair` turns the hip-fire dot on.
- `Main Camera` > **Sniper Rifle**: scope-in time, zoom, sway, breath, bolt time, hip spread, recoil.
- `Player` > **Player Controller**: speed, jump height, gravity, sensitivity, `Count Ground Spins`.

Changes made in Play mode are lost when you stop, so copy the values you like into the
defaults at the top of each script. Head size is `Target.HeadDiameter`.

## Files

| Script | Job |
| --- | --- |
| `MainMenu` | The menu, its tabs, and switching between the menu and a mode |
| `Profile` | TEMPLATE: credits, item catalogue, owned items, loadout |
| `CharacterModel` | TEMPLATE: the menu character |
| `MatchMode` | TEMPLATE: shared base for the main game modes |
| `TeamDeathmatchMode` / `EliminationMode` | TEMPLATE: the two main modes |
| `Sfx` | Placeholder sound effects made in code |
| `AimTrainer` | Training drills, round timer, target spawning, best scores |
| `ConfigPanel` | The in-world settings board |
| `Target` / `HitZone` | The dummy, its body parts, strafing |
| `SniperRifle` | Scoping, sway, breath, recoil, firing, hit detection |
| `ScoreManager` | The scoring rules and running totals |
| `PlayerController` | Movement, mouse look, spin tracking |
| `TrickshotHUD` | Score, timer, results, scope overlay, hitmarkers, shot popups |
| `TSInput` | All key and mouse bindings in one place |
| `TrickshotBootstrap` | Builds the range, menu stage and every component in code |
| `TrickshotAutoStart` | Spawns the range on Play; delete when you have your own level |

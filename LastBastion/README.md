# Last Bastion — Alpha (Level 1: The Ashen Pass)

Phoenix Studios · Brian Hunt, Aiden Tjahjono, Micheal Biaggini

A fantasy tower defense game. The Orc Warhost marches down the Ashen Pass toward the Last Bastion. Build towers, upgrade them down their trees, move Sir Aldric into the fight, and survive 10 waves ending with the warlord Gorruk Skullsplitter.

Everything (art, UI, level) is built from C# at runtime. There are no prefabs, imported art or hand-made scenes to merge, so the whole game lives in `Assets/LastBastion/`.

---

## Setup (Unity 6)

1. Open your team's Unity 6 project, or create a new one from the **Universal 2D** template (the **2D (Built-In Render Pipeline)** template also works).
2. Copy the `Assets/LastBastion` folder into the project's `Assets` folder. Put `.gitignore` at the repo root if you don't already have a Unity one.
3. Wait for Unity to compile. An editor script then does the setup automatically:
   - creates `Assets/LastBastion/Scenes/Title`, `LevelSelect` and `Level1` (empty on purpose, since the code builds each screen),
   - puts those three scenes first in **Build Settings** (Title = scene 0),
   - creates the unlit sprite material `Assets/LastBastion/Resources/LB_SpriteUnlit.mat` for your render pipeline,
   - opens the Title scene.

   If it doesn't run, use the menu **Last Bastion › Set Up Scenes and Build Settings**.
4. Press **Play** in the Title scene. You can also press Play in `Level1` to jump straight into the level.

Both input systems work. The code supports the old Input Manager, the new Input System package, or **Both**, so leave **Player › Active Input Handling** however your project has it.

### Building the executable for demo day
**File › Build Profiles** (Unity 6), pick **Windows** (or macOS), check that Title / LevelSelect / Level1 are listed first, then **Build**. Copy the build folder to both booth machines.

### Git: Alpha tag
```
git checkout master
git merge feature/alpha        # or whatever your feature branch is called
git tag Alpha
git push origin master --tags
```
Commit the `.meta` files Unity generates next to every script and scene, or the scenes will lose their references on other machines.

---

## Controls

| Input | Action |
|---|---|
| Left click open ground beside the road | Open the build menu there (then click a tower, or press 1 / 2 / 3). Green ring = OK, red = blocked |
| Left click a tower | Open its upgrade tree / sell |
| Right click | Move Sir Aldric (or select him, then left click the ground) |
| Q | Shield Slam: damage and stun orcs around Sir Aldric |
| H | Find and select Sir Aldric |
| Space | Start the next wave now (bonus gold for time skipped) |
| WASD / arrow keys | Pan camera |
| Mouse wheel | Zoom toward the cursor |
| Middle mouse drag | Pan camera |
| F | Toggle 1x / 2x speed |
| Esc | Close menu / pause |
| F9 / F10 | Debug: +500 gold / skip to the boss wave (good for demos) |

---

## Alpha requirements map

| Requirement | Where it is | What it does |
|---|---|---|
| **Camera, character and controls** | `CameraController.cs`, `Hero.cs`, `LevelManager.HandleInput` | Strategy camera with pan, zoom-to-cursor, drag and map clamping. **Sir Aldric the Unbroken** is the controllable hero: move with right click, blocks up to 3 orcs and fights them in melee, regenerates out of combat, respawns at the gate 12s after falling, and has **Shield Slam** (Q, 14s cooldown). |
| **Title screen** | `TitleScreen.cs` | The Bastion at dusk with rising embers and the Warhost's torches on the horizon. Campaign, How to Play (goals and controls), Quit. |
| **HUD mechanic** | `HUD.cs` | The command bar the game is played through: gold, lives and wave tracker; a **wave scout panel** showing the next wave's enemies with **Call Early** (Space) for bonus gold; Sir Aldric's card (HP, status, Shield Slam cooldown); the build menu at the plot; the **tower upgrade-tree panel**; the boss bar (enraged / axe-incoming states); Captain Brand's alerts; floating gold and damage popups; an eruption countdown over the vent; pause, victory and defeat screens with a 3-star rating. |
| **Level** | `LevelManager.cs`, `GameData.cs › Level1Data` | **The Ashen Pass**: a winding mountain road from the orc cave to the Bastion gate, 10 waves. **Free tower placement:** build on any open ground beside the road (within 4.5 units, not on the road, not overlapping another tower). A green/red ring previews the spot, and hovering a tower in the build menu shows its range. |
| **Level select** | `LevelSelectScreen.cs` | All three campaign battles from the GDD (Ashen Pass playable; Weeping Marsh and Siege of the Bastion locked "coming in the Beta"), saved stars per difficulty, and difficulty choice (Recruit / Warden / Veteran). |
| **Combat mechanic** | `Tower.cs`, `Projectile.cs` | Towers auto-target the orc furthest along the road. **Archer Tower** (fast, single target), **Catapult** (lobbed splash that can miss fast enemies), **Mage Tower** (magic ignores armor). Each has a **3 × 3 upgrade tree** (Power / Speed / Utility); one branch can reach Tier 3 and a second Tier 2. Upgrades add abilities: piercing, armor-piercing, slow, burn, stun, Hunter's Mark, chain lightning, Dispel, Arrow Storm, Meteor, Overcharge, Siege Engine. Armor, physical / magic / true damage. |
| **Enemy with a unique mechanic** | `Enemy.cs` (War Drummer) | **War Drummer**: its drum aura slows every tower within 3.5 units by 30% (towers show a red ring and the panel says "Slowed"). Kill it first, or Dispel it with a Mage Tower to silence it. Also in the level: Orc Grunts and fast **Wolf Riders**. |
| **Hazard with a unique mechanic** | `EmberVent.cs` | **Ember Vent**: a crack in the road on a telegraphed cycle (dormant, then 2.5s of glowing / pulsing / countdown, then 2.5s eruption). It is **neutral**: it burns orcs *and* Sir Aldric. Park the hero just past the vent so blocked orcs stand in the fire, then pull him out before it blows. |
| **Boss with 2 unique mechanics** | `BossGorruk.cs` | **Gorruk Skullsplitter, Orc Warlord** (wave 10). **1. Axe Throw:** every 9s he picks the tower with the most gold invested, shows a red telegraph line for 1s, then hurls his axe and knocks the tower out for 5s. **2. War Cry:** at half health he roars, and every orc on the field moves 50% faster (Dispel strips it). |

---

## Tower building animation (Warden)

Buying a tower now plays a construction animation instead of the tower appearing instantly. A Warden appears beside the plot and swings a hammer. At the impact (0.35s) the magic particles burst and the tower pops in. The Warden leaves at 0.9s. The gold is spent on click and the plot is locked while it builds. The timing lives in `Scripts/Towers/TowerConstruction.cs` (`ImpactTime`, `BuildDuration`).

**To use your animated Warden:** in Unity, move (drag in the Project window, don't copy outside Unity) `Warden_Build.prefab` into `Assets/LastBastion/Resources/`, keeping the name `Warden_Build`. Unity keeps its sprite, Animator controller, material and nested `Buidling_Particle_System` links. The game loads it automatically. If the prefab isn't there, a code-drawn placeholder Warden plays the same timing.

Don't add the old `BuildManager.cs` to this project. It depends on `GameManager`, `MapBuilder` and `SpriteFactory` from the earlier prototype and won't compile here. `TowerConstruction.cs` replaces its `BuildTower` coroutine. `WardenBuildEffect.cs` is fine to keep; if it's on the prefab, the game turns it off so the timings don't fight.

---

## Tuning
All balance numbers are in `Scripts/Data/GameData.cs`: placement rules (`TowerFootprint`, `MinRoadDistance`, `MaxRoadDistance`), start gold, wave timers, enemy HP/speed/gold, tower stats, upgrade costs and the wave list. Hero numbers are constants at the top of `Hero.cs`, and the vent's cycle is at the top of `EmberVent.cs`.

## Alpha notes / known limitations
- Placeholder art and no audio yet. All sprites are generated shapes in `Art.cs`, ready to be swapped for real art.
- Alpha pacing differs from the GDD: 10 waves instead of 30. Wolf Riders debut on wave 3 and War Drummers on wave 5 so a short booth demo sees everything.
- Levels 2 and 3 and the Hero Tower are planned for the Beta. In this build Sir Aldric starts on the field.

## File layout
```
Assets/LastBastion/
  Editor/LastBastionSetup.cs     auto-creates scenes, build settings, sprite material
  Scripts/Core/                  input wrapper, generated art, effects, UI kit, scene routing, session
  Scripts/Data/GameData.cs       enemies, towers, upgrades, balance, Level 1 layout and waves
  Scripts/Level/                 level manager, road, plots, Ember Vent, camera
  Scripts/Units/                 Enemy, BossGorruk, Hero
  Scripts/Towers/                Tower, Projectile
  Scripts/UI/                    TitleScreen, LevelSelectScreen, HUD
```

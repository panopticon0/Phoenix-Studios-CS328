# Last Bastion — Beta

Phoenix Studios · Brian Hunt, Aiden Tjahjono, Micheal Biaggini

A fantasy tower defense game. The Orc Warhost marches down the Ashen Pass toward the Last Bastion. Drag towers onto the battlefield, upgrade them down their paths, command your hero (Sir Aldric or Kaela Swiftwind), and survive **30 waves** ending with the warlord Gorruk Skullsplitter. New players can start with the **Training Grounds** tutorial.

Everything (art, UI, audio, levels) is built from C# at runtime. There are no prefabs, imported art or hand-made scenes to merge, so the whole game lives in `Assets/LastBastion/`.

---

## Setup (Unity 6)

1. Open the project (`LastBastion/`) in Unity 6. An editor script creates the Title, LevelSelect and Level1 scenes, adds them to Build Settings (Title first) and creates the sprite material. If it doesn't run, use **Last Bastion › Set Up Scenes and Build Settings**.
2. Press **Play** in the Title scene.
3. Required built-in packages (already in `Packages/manifest.json`): Audio, Particle System, Animation, IMGUI.

### Build the executable for demo day
**File › Build Profiles** › Windows › make sure Title / LevelSelect / Level1 are listed (Title first) › **Build** into a folder outside the project. Copy the **whole** build folder to both booth machines.

### Git: Beta tag
```
git checkout -b feature/beta
git add -A
git commit -m "Beta"
git checkout main
git merge feature/beta
git tag Beta
git push origin main --tags
```
Commit the `.meta` files Unity generates for the new scripts.

---

## Controls

| Input | Action |
|---|---|
| **Drag** a tower card from the shop (right) onto the map | Build it there (green ring = OK, red = blocked). Or click the card, then click the map |
| 1 / 2 / 3 | Pick Archer / Catapult / Mage to place |
| Left click a tower | Upgrade panel (3 paths, targeting priority, sell) |
| Left click the hero, the portrait, or H | Hero panel (level, XP, skill tree) |
| Right click | Move the hero |
| Q | Hero ability: Shield Slam (Aldric) / Pinning Volley (Kaela). Damage + stun; interrupts boss wind-ups |
| 4 / E, or drag the Dragonfire card | Dragonfire power-up |
| R, or click the cliff | Trigger the Rockslide |
| Space | Start / call the next wave (early call = bonus gold) |
| WASD / arrows, mouse wheel, middle-drag | Pan and zoom |
| F / Esc / M | 2x speed / pause (volume sliders inside) / mute |
| F9 / F10 | Debug: +500 gold & a Dragonfire charge / **skip to the boss wave (wave 30)** |

---

## Beta requirements map

| Requirement | Where | What it does |
|---|---|---|
| **Everything from Part 3 (Alpha)** | all | Camera/hero/controls, title screen, HUD, level, level select, tower combat, War Drummer, Ember Vent and Gorruk are all still in and expanded. |
| **HUD mechanic** | `UI/HUD.cs` | **Bloons-style command HUD.** A shop sidebar you **drag towers and the power-up from** onto the map, with live placement ghosts, range previews and cost/reason labels. A contextual **upgrade panel that docks on the side of the screen away from the selected tower**, showing three stacked upgrade paths with BUY prices, a **targeting priority selector (First / Last / Strong / Close)**, kill count and sell. The hero card has a **radial Shield Slam cooldown clock**, a **respawn clock**, level/XP and a hero-point badge, and opens Sir Aldric's skill-tree panel. A wave box shows a scout preview with Start / Call Early, plus a boss bar with live wind-up warnings. Compact advisor toasts sit at the bottom-center. |
| **New combat mechanic** | `Units/Enemy.cs` (Stun / Interrupt), `BossGorruk.cs`, `BossBrakka.cs` | **Interrupts.** Bosses telegraph their big abilities with a visible wind-up (ring, line, bar and a "STUN TO INTERRUPT!" label). Any stun during a wind-up cancels the ability and **Staggers** the boss (+25% damage taken for 3s). Stun sources: Shield Slam, Catapult Earthshaker, the Rockslide. |
| **2 new enemies, 1 unique mechanic each** | `Units/Enemy.cs`, `Data/GameData.cs` | **Goblin Sneak: invisible.** Towers can't target it without sight (Archer Eagle Eye, Mage True Sight, Catapult Spotter Crow). Sir Aldric spots them up close, and splash, Dragonfire, the vent and the rockslide still hit them. **Berserker: enrage.** It gets faster and hits harder the more it's hurt, up to 2.5x speed. *Bonus:* Ironhide (heavy armor), **Warchief Brakka** (wave 10 boss: interruptible Rally Horn heal) and **Troll Juggernaut** (wave 20 boss: regenerates). |
| **2 new hazards, 1 unique mechanic each** | `Level/Rockslide.cs`, `Level/AshStorm.cs` | **Rockslide Cliff: player-triggered.** Click the cliff (or R) to send three boulders down a fixed lane across the road. They crush and stun orcs (interrupting bosses) but also hurt Sir Aldric if he's in the lane, then the cliff takes 40s to rebuild. **Ash Storm: blinds towers.** A drifting ash cloud crosses the map every ~45s, and towers inside it lose 40% range until it passes. |
| **1 power-up** | `Towers/DragonfireStrike.cs`, `Level/LevelManager.cs` | **Dragonfire.** Orcs drop **Ember Shards** that fly into the Dragonfire card (max 3 charges). Drag the card onto the map: a dragon's shadow sweeps over and breathes fire for 180 magic damage, leaves burning ground for 4s, and reveals invisible enemies. |
| **Music / sound effects** | `Core/Audio.cs` | An AudioManager with three procedural music loops (title, battle, boss, cross-faded) and around 30 synthesized sound effects (build hammer, arrows, catapult, magic, hits, deaths, coins, shards, wave horn, boss roar, axe, eruption, rockslide, dragonfire, slam, level up, interrupt, victory/defeat...). There are volume sliders and mute in the pause menu. **Drop a real `.wav`/`.ogg` with the same name into `Assets/LastBastion/Resources/Audio/` to replace any sound** (names are in `Sfx` and `Music`). |
| **All 30 waves** | `Data/GameData.cs › Levels.AshenPass` | 30 designed waves. New enemies debut at 4 (Wolf Rider), 7 (Ironhide), 11 (War Drummer), 14 (Goblin Sneak) and 18 (Berserker). Bosses at 10 (Brakka), 20 (Troll) and 30 (Gorruk). Orcs get 4.5% tougher each wave. **F10 still skips to the boss wave.** |

## Heroes and hero select

Pressing **Play** on a campaign level opens **Choose Your Hero** (stats, ability and level-up tree side by side; the last pick is remembered). The **Training Grounds always use Sir Aldric**.

| Hero | Style | Ability (Q) | Level-up tree |
|---|---|---|---|
| **Sir Aldric the Unbroken** | Melee blocker: holds up to 3 enemies on the road, 420 HP, spots invisible enemies up close (2.4u) | **Shield Slam**: 45 damage + 2s stun around him | Valor (damage, Cleave, Holy Blade) · Bulwark (HP, Shield Wall, Last Stand) · Command (cooldown, Thunder Slam, Rally Cry) |
| **Kaela Swiftwind** | Ranged ranger: shoots the enemy furthest down the road within 4.6u, doesn't block (enemies that walk into her land glancing blows), 260 HP, reveals invisible enemies in a wide 4.4u radius | **Pinning Volley**: 40 damage + 1.5s stun on the group she's aiming at | Marksman (damage, Piercing Shot, Elven Arrows) · Fleetfoot (attack speed, Long Stride, Twin Shot) · Huntress (cooldown, Barbed Volley, Hunter's Mark) |

**Level-up panel:** whenever the hero levels up, a panel appears beside the hero card saying what changed (+1 hero point, XP to the next level) with one-click **Learn** buttons for the next upgrade in each branch (hover for details), plus **Full tree [H]** and **Later**. It stays until the points are spent or dismissed.

**Tutorial objectives** now sit in a card right beside the hero card at the bottom of the screen instead of over the top of the road. Advisor toasts and key hints share that bottom row.

Hero numbers live in `HeroData.Aldric` / `HeroData.Kaela` in `Scripts/Data/GameData.cs`; behavior is in `Scripts/Units/Hero.cs`.

## Level 2: The Weeping Marsh

30 waves against the **Undead Legion** on a longer, winding road through fog-drowned graveyards (Level Select › Level 2). It has its own scenery (black pools, reeds, gravestones, dead trees, a sunken crypt) and an eerie music track.

| Enemy | Debut | Unique mechanic |
|---|---|---|
| Skeleton | 1 | Weak but relentless (also raised by Necromancers and Bone Walls) |
| Ghoul | 4 | **Lurches**: shambles, then bursts forward at 2.5x speed |
| Wraith | 7 | **Invisible + ethereal**: needs sight, and takes half non-magic damage while unseen |
| Banshee | 9 | **Wail**: locks onto the nearest tower and **freezes** it for 3s (interruptible) |
| Necromancer | 12 | **Raise Dead**: stops to raise 2 Skeletons (interruptible) |
| Bone Golem | 16 | Armored; **shatters into 4 Skeletons** on death |
| **Death Knight Varn** (boss) | 10 | **Death Charge**: winds up, then charges at 4x speed and tramples through Sir Aldric's block (interruptible) |
| **Plague Abomination** (boss) | 20 | Sheds a Ghoul every 6s and **bursts into 5 Ghouls** on death |
| **Vesperine, the Lich Queen** (final boss) | 30 | **Fade**: winds up, then turns invisible for 5s (interruptible). **Bone Wall**: raises 5 Skeletons at 75/50/25% health |

The marsh versions of the hazards use the same mechanics with new looks: **Marsh Gas** (green flame that burns both sides and reveals Wraiths), the **Deadfall** (click the rotten tree to roll logs down its lane) and **Marsh Fog** (towers inside lose 40% range). Raised and split-off undead are worth 1 gold each, so they can't be farmed. F10 skips to Vesperine.

## Playtest feedback addressed

| Feedback | Change |
|---|---|
| Make player more noticeable | Sir Aldric is 25% bigger, with a pulsing golden halo, a spinning gold ring, a name/level plate, and an **off-screen arrow** at the screen edge (click it to jump to him). |
| More explanations, tutorial level | New **Training Grounds** level: 9 guided objectives (shown beside the hero card, off the road) with arrows to the exact HUD element and glowing map markers, and waves that wait for you. Plus tooltips on every shop card and the Slam button, intros for every new enemy, first-time tips for each hazard, and road direction arrows. |
| Indicator for hero respawn | A gold **respawn clock** at the gate with a countdown label, and a clock and seconds on his portrait. |
| Drag menu for towers | Bloons-style **drag-and-drop shop** (or click a card, then click the map). When a tower's upgrade panel opens on the right, it replaces the shop (like Bloons), so BUY clicks always reach the upgrade. |
| Hero upgrade tree | Heroes earn XP from every kill (double for their own), level to 10, and spend **hero points** in three branches (9 upgrades each). A **level-up panel** explains each level and lets you learn in one click. A second hero, **Kaela Swiftwind**, is picked on the new hero select. |
| Better cooldown indicator for hero ability | A large round Slam button with a **radial clock**, seconds left, a "SLAM READY" pulse, and a ready ring that pulses around Sir Aldric in the world. A slam-radius preview shows while he's selected. |
| Move popup positions | Advisor messages are now **compact toasts at the bottom-center**, between the hero card and the shop, with a dismiss "x". |
| Stun cancels boss skills | That's the new **Interrupt** mechanic (above). |
| Ability visual cues | Boss wind-up rings, lines and "STUN TO INTERRUPT!" labels, a wind-up bar on the boss HUD, tower **ability charge clocks** (Arrow Storm / Meteor / Overcharge), Rally glow, a Drummer slow ring, and an Ash Storm label. |
| Coin drop / enemy death effects | **Coins burst out of every defeated orc and fly into the gold counter** (it pulses). Orcs topple, flatten and fade, with sparks. Ember Shards fly into the Dragonfire card. |

## Tuning
All balance numbers are in `Scripts/Data/GameData.cs`: `Balance` (gold, timers, placement, interrupts, hazards, Dragonfire), `EnemyData`, `TowerData`, `HeroData` (XP curve and tree), and `Levels` (waves). Hazard timings are at the top of `EmberVent.cs`, `Rockslide.cs` and `AshStorm.cs`.

## Notes
- Placeholder art: all sprites are generated shapes in `Art.cs`. Placeholder audio is synthesized in `Audio.cs`, and each sound can be replaced by a file.
- The Warden building animation uses `Assets/LastBastion/Resources/Warden_Build.prefab` if you add it (otherwise a code-drawn Warden plays the same timing).
- Level 3 (Siege of the Bastion) is shown locked on Level Select.

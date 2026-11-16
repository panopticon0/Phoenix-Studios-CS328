using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    public enum GameState { Playing, Paused, Victory, Defeat }

    public class Popup
    {
        public Vector3 World;
        public string Text;
        public Color Color;
        public float Age;
    }

    public class Announcement
    {
        public string Title, Body, Speaker;
        public Color Accent;
        public float Duration, Age;
    }

    /// <summary>
    /// Runs a level (the Ashen Pass or the Training Grounds): builds the map, runs waves, tracks gold,
    /// lives and power-up charges, handles world input (Bloons-style drag placement, selection,
    /// hero orders) and owns the win/lose flow.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager I { get; private set; }

        public LevelDef Level { get; private set; }
        public PathRoute Route { get; private set; }
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Tower> Towers = new List<Tower>();
        public readonly List<BuildPlot> Plots = new List<BuildPlot>();
        public readonly List<Popup> Popups = new List<Popup>();
        public readonly List<Announcement> Announcements = new List<Announcement>();
        public readonly List<EmberVent> Vents = new List<EmberVent>();

        public Hero HeroUnit { get; private set; }
        public Rockslide Cliff { get; private set; }
        public AshStorm Storm { get; private set; }
        public Camera Cam { get; private set; }
        public CameraController CamCtl { get; private set; }
        public Enemy Boss { get; private set; }
        public TutorialDirector Tutorial { get; private set; }

        public int Gold { get; private set; }
        public int Lives { get; private set; }
        public int MaxLives { get; private set; }
        public int WaveNumber { get; private set; }
        public int WaveCount => Level.Waves.Length;
        public float NextWaveTimer { get; private set; }
        public bool WaveCountdown { get; private set; }
        public bool AutoWaves => !Level.IsTutorial;
        public bool Spawning => activeSpawners > 0;
        public GameState State { get; private set; }
        public bool FastForward { get; private set; }
        public float ElapsedTime { get; private set; }
        public int Kills { get; private set; }
        public int StarsEarned { get; private set; }
        public int PowerCharges { get; private set; }
        public float GoldPulse { get; private set; }
        public float PowerPulse { get; private set; }

        // Tutorial tracking
        public int UpgradesBought { get; private set; }
        public bool SlamUsed { get; private set; }
        public bool PowerUsed { get; private set; }
        public int InterruptCount { get; private set; }

        public Tower SelectedTower { get; private set; }
        public bool HeroSelected { get; private set; }
        public bool HasSelection => SelectedTower != null || HeroSelected;

        // Bloons-style placement
        public bool Placing { get; private set; }
        public bool PlacingPower { get; private set; }
        public TowerType PlacingType { get; private set; }
        public bool PlacementValid { get; private set; }
        public string PlacementReason { get; private set; }
        bool placeDrag;
        Vector2 placeStartMouse;

        /// <summary>GUI positions of the HUD gold counter and Dragonfire card (set by the HUD), for flying coins.</summary>
        public Vector2 GoldIconGui = new Vector2(40f, 32f);
        public Vector2 PowerIconGui = new Vector2(1800f, 600f);

        public int EarlyBonus => WaveNumber == 0 || !GameSession.EarlyCallBonus || !AutoWaves ? 0 : Mathf.CeilToInt(NextWaveTimer) * Balance.EarlyBonusPerSecond;
        public string NextWavePreview => WaveNumber < WaveCount ? Level.Waves[WaveNumber].Summary() : "";
        public bool NextWaveIsBoss => WaveNumber < WaveCount && Level.Waves[WaveNumber].HasBoss;
        public bool Marsh => Level != null && Level.Theme == LevelTheme.Marsh;
        string BattleTrack => Marsh ? Music.Marsh : Music.Battle;

        public static readonly Rect MapBounds = new Rect(-20f, -12f, 40f, 24f);
        static readonly Rect WalkBounds = new Rect(-18f, -9.5f, 35f, 19f);
        static readonly Rect BuildBounds = new Rect(-17.4f, -9.6f, 33.2f, 19.2f);

        int activeSpawners;
        bool ventTipShown, heroTipShown, stormTipShown, rockTipShown, interruptTipShown, shardTipShown;
        Transform world;
        SpriteRenderer gateFlash;
        Transform ghost;
        SpriteRenderer ghostBase, ghostRoof, ghostRange;
        readonly HashSet<EnemyType> seenTypes = new HashSet<EnemyType>();

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            Level = Levels.Get(GameSession.SelectedLevel);
            world = new GameObject("World").transform;
            Cam = Art.MakeCamera(Level.Theme == LevelTheme.Marsh ? Palette.Hex("#18221E") : Palette.GroundDark, 10.8f);
            CamCtl = Cam.gameObject.AddComponent<CameraController>();
            CamCtl.Init(Cam, MapBounds);

            Route = new PathRoute(Level.Path);
            BuildMap();
            int heroId = Level.IsTutorial ? HeroData.AldricId : GameSession.SelectedHero;
            HeroUnit = Hero.Create(world, Level.HeroStart, Level.HeroRespawn, HeroData.Get(heroId));

            Gold = Level.StartGold;
            MaxLives = Lives = GameSession.StartLives;
            PowerCharges = Balance.DragonfireStartCharges;
            WaveNumber = 0;
            WaveCountdown = true;
            NextWaveTimer = Balance.FirstWaveDelay;
            State = GameState.Playing;

            gameObject.AddComponent<HUD>();
            AudioManager.PlayMusic(BattleTrack);

            if (Level.IsTutorial)
            {
                Tutorial = gameObject.AddComponent<TutorialDirector>();
            }
            else if (Marsh)
            {
                Announce(Level.Name, "The dead are rising from the marsh, Commander. Undead shrug off nothing, but Wraiths hide and Banshees freeze towers. Bring sight and magic.", "Captain Brand", Palette.Undead, 9f);
            }
            else
            {
                Announce(Level.Name, "Drag towers from the shop on the right onto the ground beside the road. Right-click to move " + HeroUnit.Name + ". Waves start on their own, or press Space.", "Captain Brand", Palette.Ember, 9f);
            }
        }

        // ------------------------------------------------------------------ Map

        void BuildMap()
        {
            Random.InitState(1107 + Level.Id);
            bool marsh = Marsh;
            Color groundC = marsh ? Palette.Hex("#26332C") : Palette.Ground;
            Color patchC = marsh ? new Color(0.08f, 0.13f, 0.11f, 0.5f) : new Color(0.12f, 0.09f, 0.07f, 0.45f);
            Color glowC = marsh ? new Color(0.45f, 0.95f, 0.55f, 0.13f) : new Color(0.9f, 0.35f, 0.1f, 0.18f);
            Color roadEdgeC = marsh ? Palette.Hex("#3A3226") : Palette.RoadEdge;
            Color roadC = marsh ? Palette.Hex("#5E5040") : Palette.Road;
            Color pebbleC = marsh ? new Color(0.3f, 0.27f, 0.2f, 0.6f) : new Color(0.45f, 0.36f, 0.27f, 0.6f);
            Art.Make("ground", world, Art.Square, groundC, Vector2.zero, new Vector2(MapBounds.width + 4f, MapBounds.height + 4f), 0);
            for (int i = 0; i < 26; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19f, 19f), Random.Range(-11f, 11f));
                float s = Random.Range(3f, 7f);
                Art.Make("patch", world, Art.Glow, patchC, p, new Vector2(s, s * 0.7f), 1);
            }
            for (int i = 0; i < 14; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19f, 19f), Random.Range(-11f, 11f));
                if (Route.DistanceTo(p) < 2f) continue;
                Art.Make(marsh ? "wisp" : "emberPatch", world, Art.Glow, glowC, p, Vector2.one * Random.Range(1.5f, 3f), 1);
            }

            var pts = Level.Path;
            // Build zone: a faint band along the road shows where towers can go.
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                Vector2 dir = (b - a).normalized;
                Art.Segment("buildZone", world, a - dir * Balance.MaxRoadDistance, b + dir * Balance.MaxRoadDistance, Balance.MaxRoadDistance * 2f, new Color(0.9f, 0.6f, 0.3f, 0.035f), 1);
            }
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                Vector2 dir = (b - a).normalized;
                Art.Segment("roadEdge", world, a - dir * 1f, b + dir * 1f, 2.1f, roadEdgeC, 2);
            }
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                Vector2 dir = (b - a).normalized;
                Art.Segment("road", world, a - dir * 0.8f, b + dir * 0.8f, 1.7f, roadC, 3);
            }
            for (float d = 0f; d < Route.Length; d += 0.9f)
            {
                Vector2 p = Route.PositionAt(d) + Random.insideUnitCircle * 0.55f;
                Art.Make("pebble", world, Art.Circle, pebbleC, p, Vector2.one * Random.Range(0.08f, 0.18f), 3);
            }
            // Direction arrows on the road so new players see where orcs go
            for (float d = 3f; d < Route.Length - 2f; d += 6f)
            {
                Vector2 p = Route.PositionAt(d), dir = Route.DirectionAt(d);
                Art.Make("roadArrow", world, Art.Triangle, new Color(0.35f, 0.26f, 0.18f, 0.6f), p, new Vector2(0.45f, 0.5f), 3, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            }

            if (marsh) BuildMarshDecor();
            for (int i = 0; i < (marsh ? 25 : 70); i++)
            {
                Vector2 p = new Vector2(Random.Range(-19.5f, 19.5f), Random.Range(-11.5f, 11.5f));
                if (Route.DistanceTo(p) < 1.6f || Vector2.Distance(p, Level.Gate) < 3f) continue;
                if (Level.HasRockslide && (PathRoute.DistanceToSegment(p, Level.SlideFrom, Level.SlideTo) < 1.3f || Vector2.Distance(p, Level.CliffPosition) < 2.5f)) continue;
                float s = Random.Range(0.25f, 0.9f);
                Art.Make("rockShadow", world, Art.Circle, new Color(0f, 0f, 0f, 0.3f), p + new Vector2(0.06f, -0.08f), new Vector2(s * 1.1f, s * 0.8f), 1);
                Art.Make("rock", world, Art.Circle, Color.Lerp(Palette.Rock, Palette.StoneDark, Random.value), p, new Vector2(s, s * 0.8f), 1);
            }

            // Cave where the Warhost emerges
            Vector2 cave = pts[0];
            if (marsh)
            {
                // A sunken crypt the dead crawl out of
                Art.Make("cryptGlow", world, Art.Glow, new Color(0.4f, 1f, 0.5f, 0.45f), cave + Vector2.left * 1.2f, new Vector2(5f, 5f), 4);
                Art.Make("crypt", world, Art.Square, Palette.Hex("#3A403C"), cave + Vector2.left * 1.8f, new Vector2(2.6f, 3.2f), 5);
                Art.Make("cryptDoor", world, Art.Square, Palette.Hex("#0E1410"), cave + Vector2.left * 1.3f, new Vector2(1.2f, 1.8f), 6);
                Art.Make("cryptTop", world, Art.Triangle, Palette.Hex("#4A524C"), cave + new Vector2(-1.8f, 2.1f), new Vector2(2.8f, 1.2f), 6);
            }
            else
            {
                Art.Make("caveGlow", world, Art.Glow, new Color(0.8f, 0.2f, 0.1f, 0.5f), cave + Vector2.left * 1.2f, new Vector2(5f, 5f), 4);
                Art.Make("cave", world, Art.Circle, Palette.Hex("#120D0B"), cave + Vector2.left * 1.6f, new Vector2(3.4f, 3.4f), 5);
            }

            // Hazards
            foreach (var v in Level.Vents) Vents.Add(EmberVent.Create(world, v));
            if (Level.HasRockslide) Cliff = Rockslide.Create(world, Level.CliffPosition, Level.SlideFrom, Level.SlideTo);
            if (Level.HasAshStorm) Storm = AshStorm.Create(world);

            // Placement ghost
            ghost = new GameObject("PlacementGhost").transform;
            ghost.SetParent(world, false);
            ghostRange = Art.Make("ghostRange", ghost, Art.Ring, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, Vector2.one, 30);
            Art.Make("ghostRangeFill", ghostRange.transform, Art.Circle, new Color(1f, 1f, 1f, 0.08f), Vector2.zero, Vector2.one, 29);
            ghostBase = Art.Make("ghostBase", ghost, Art.Square, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, Vector2.one * 1.2f, 31);
            ghostRoof = Art.Make("ghostRoof", ghost, Art.Circle, new Color(1f, 1f, 1f, 0.8f), Vector2.zero, Vector2.one * 0.7f, 32);
            ghost.gameObject.SetActive(false);

            // The Last Bastion (gate on the right edge)
            Vector2 g = Level.Gate;
            Art.Make("wallShadow", world, Art.Square, new Color(0f, 0f, 0f, 0.35f), new Vector2(g.x + 1.05f, 0f), new Vector2(1.3f, 26f), 6);
            Art.Make("wall", world, Art.Square, Palette.Stone, new Vector2(g.x + 0.9f, 0f), new Vector2(1.1f, 26f), 7);
            for (float y = -12f; y <= 12f; y += 0.8f)
                Art.Make("crenel", world, Art.Square, Palette.StoneDark, new Vector2(g.x + 0.45f, y), new Vector2(0.3f, 0.4f), 8);
            Art.Make("keepShadow", world, Art.Square, new Color(0f, 0f, 0f, 0.4f), new Vector2(g.x + 1.6f, g.y - 0.15f), new Vector2(3f, 3.4f), 8);
            Art.Make("keep", world, Art.Square, Palette.Hex("#B0A594"), new Vector2(g.x + 1.4f, g.y), new Vector2(2.8f, 3.2f), 9);
            Art.Make("keepTop", world, Art.Square, Palette.Stone, new Vector2(g.x + 1.4f, g.y), new Vector2(2.2f, 2.6f), 9);
            Art.Make("gate", world, Art.Square, Palette.Hex("#4A3020"), new Vector2(g.x + 0.2f, g.y), new Vector2(0.5f, 1.8f), 10);
            Art.Make("brazierGlow", world, Art.Glow, new Color(1f, 0.6f, 0.2f, 0.7f), new Vector2(g.x + 1.4f, g.y), new Vector2(2.5f, 2.5f), 10);
            Art.Make("brazier", world, Art.Circle, Palette.Gold, new Vector2(g.x + 1.4f, g.y), new Vector2(0.5f, 0.5f), 10);
            Art.Make("bannerTop", world, Art.Triangle, Palette.Ember, new Vector2(g.x + 1.4f, g.y + 2.1f), new Vector2(0.7f, 0.9f), 10, 180f);
            Art.Make("bannerBottom", world, Art.Triangle, Palette.Ember, new Vector2(g.x + 1.4f, g.y - 2.1f), new Vector2(0.7f, 0.9f), 10);
            gateFlash = Art.Make("gateFlash", world, Art.Glow, new Color(1f, 0.15f, 0.1f, 0f), new Vector2(g.x + 0.6f, g.y), new Vector2(5f, 5f), 11);
        }

        bool Clear(Vector2 p, float roadGap)
        {
            if (Route.DistanceTo(p) < roadGap || Vector2.Distance(p, Level.Gate) < 3.5f) return false;
            if (Level.HasRockslide && (PathRoute.DistanceToSegment(p, Level.SlideFrom, Level.SlideTo) < 1.6f || Vector2.Distance(p, Level.CliffPosition) < 2.6f)) return false;
            foreach (var v in Level.Vents) if (Vector2.Distance(p, v) < 2.2f) return false;
            return true;
        }

        /// <summary>Weeping Marsh scenery: black pools, reeds, gravestones and dead trees.</summary>
        void BuildMarshDecor()
        {
            for (int i = 0; i < 14; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19f, 19f), Random.Range(-11f, 11f));
                float s = Random.Range(1.6f, 3.4f);
                if (!Clear(p, s * 0.5f + 1.3f)) continue;
                Art.Make("poolRim", world, Art.Circle, Palette.Hex("#2E4A44"), p, new Vector2(s * 1.15f, s * 0.75f), 1);
                Art.Make("pool", world, Art.Circle, Palette.Hex("#142A2C"), p, new Vector2(s, s * 0.62f), 1);
                Art.Make("shine", world, Art.Circle, new Color(0.6f, 0.8f, 0.8f, 0.15f), p + new Vector2(-s * 0.2f, s * 0.08f), new Vector2(s * 0.3f, s * 0.1f), 1);
                for (int k = 0; k < 5; k++)
                {
                    Vector2 q = p + Random.insideUnitCircle.normalized * s * 0.55f + Random.insideUnitCircle * 0.2f;
                    Art.Make("reed", world, Art.Square, Palette.Hex("#4E6A3A"), q, new Vector2(0.06f, Random.Range(0.4f, 0.8f)), 2, Random.Range(-15f, 15f));
                }
            }
            for (int i = 0; i < 26; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19f, 19f), Random.Range(-11f, 11f));
                float road = Route.DistanceTo(p);
                if (road > 7f || !Clear(p, 1.6f)) continue;
                Art.Make("graveShadow", world, Art.Square, new Color(0f, 0f, 0f, 0.35f), p + new Vector2(0.06f, -0.1f), new Vector2(0.5f, 0.66f), 1);
                Art.Make("grave", world, Art.Square, Palette.Hex("#6E7472"), p, new Vector2(0.46f, 0.62f), 1);
                Art.Make("graveTop", world, Art.Circle, Palette.Hex("#6E7472"), p + new Vector2(0f, 0.3f), new Vector2(0.46f, 0.3f), 1);
                Art.Make("graveCross", world, Art.Square, Palette.Hex("#4A504E"), p + new Vector2(0f, 0.08f), new Vector2(0.06f, 0.3f), 1);
            }
            for (int i = 0; i < 12; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19.5f, 19.5f), Random.Range(-11.5f, 11.5f));
                if (!Clear(p, 2.2f)) continue;
                float rot = Random.Range(0f, 180f);
                Art.Make("treeShadow", world, Art.Circle, new Color(0f, 0f, 0f, 0.35f), p + new Vector2(0.1f, -0.15f), new Vector2(1.2f, 0.8f), 1);
                Art.Make("trunk", world, Art.Circle, Palette.Hex("#3A2E22"), p, Vector2.one * 0.7f, 2);
                Art.Make("branch", world, Art.Square, Palette.Hex("#33281C"), p, new Vector2(1.8f, 0.12f), 2, rot);
                Art.Make("branch", world, Art.Square, Palette.Hex("#33281C"), p, new Vector2(1.3f, 0.1f), 2, rot + 70f);
            }
        }

        /// <summary>Spawn an enemy part-way along the road (Necromancer skeletons, Bone Golem shards, Abomination ghouls).</summary>
        public Enemy SpawnAt(EnemyType type, float distance, int wave, bool summoned)
        {
            if (State == GameState.Victory || State == GameState.Defeat) return null;
            var e = Enemy.Create(type, world, Route, wave);
            e.PlaceAt(distance, summoned);
            Enemies.Add(e);
            return e;
        }

        /// <summary>
        /// Free placement rule: a tower can go on any open ground beside the road, not on it, not too
        /// far from it, not in a hazard, and not overlapping another tower.
        /// </summary>
        public bool CanBuildAt(Vector2 p, out string reason)
        {
            reason = "";
            if (!BuildBounds.Contains(p)) { reason = "Can't build here"; return false; }
            float road = Route.DistanceTo(p);
            if (road < Balance.MinRoadDistance) { reason = "Can't build on the road"; return false; }
            if (road > Balance.MaxRoadDistance) { reason = "Too far from the road"; return false; }
            foreach (var v in Vents)
                if (Vector2.Distance(p, v.Position) < EmberVent.Radius + Balance.TowerFootprint) { reason = "Too close to the Ember Vent"; return false; }
            if (Cliff != null)
            {
                if (Cliff.DistanceToLane(p) < Rockslide.LaneHalfWidth + Balance.TowerFootprint) { reason = "That's in the Rockslide's path"; return false; }
                if (Cliff.HitTest(p)) { reason = "Can't build on the cliff"; return false; }
            }
            if (Vector2.Distance(p, Level.Gate) < 2.5f) { reason = "Too close to the gate"; return false; }
            foreach (var plot in Plots)
            {
                if (plot == null) continue;
                if (Vector2.Distance(plot.Position, p) < Balance.TowerFootprint * 2f) { reason = "Too close to another tower"; return false; }
            }
            return true;
        }

        // ------------------------------------------------------------------ Loop

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            if (Announcements.Count > 0)
            {
                Announcements[0].Age += udt;
                if (Announcements[0].Age >= Announcements[0].Duration) Announcements.RemoveAt(0);
            }
            GoldPulse = Mathf.Max(0f, GoldPulse - udt * 3f);
            PowerPulse = Mathf.Max(0f, PowerPulse - udt * 2f);

            HandleInput();

            if (gateFlash != null && gateFlash.color.a > 0f)
                gateFlash.color = new Color(1f, 0.15f, 0.1f, Mathf.Max(0f, gateFlash.color.a - udt * 1.5f));

            if (State != GameState.Playing) return;
            float dt = Time.deltaTime;
            ElapsedTime += dt;

            if (AutoWaves && WaveCountdown && WaveNumber < WaveCount)
            {
                NextWaveTimer -= dt;
                if (NextWaveTimer <= 0f) StartNextWave(false);
            }

            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                Popups[i].Age += dt;
                Popups[i].World += Vector3.up * dt * 0.8f;
                if (Popups[i].Age > 1.4f) Popups.RemoveAt(i);
            }

            if (WaveNumber >= WaveCount && !Spawning && Enemies.Count == 0) Win();
        }

        // ------------------------------------------------------------------ Waves

        public bool CanStartWave => State == GameState.Playing && WaveCountdown && WaveNumber < WaveCount && (Tutorial == null || Tutorial.AllowWaveStart);

        public void StartNextWave(bool calledEarly)
        {
            if (!CanStartWave)
            {
                if (calledEarly && Tutorial != null && !Tutorial.AllowWaveStart) AudioManager.Play(Sfx.Error, 0.6f);
                return;
            }

            if (calledEarly && EarlyBonus > 0)
            {
                int bonus = EarlyBonus;
                AddGold(bonus);
                AddPopup(Level.Gate + Vector2.up * 2.5f, "+" + bonus + " gold (early call)", Palette.Gold);
            }
            if (WaveNumber > 0) AddGold(Balance.WaveBounty(WaveNumber));

            WaveNumber++;
            WaveCountdown = false;
            AudioManager.Play(Sfx.Horn, 0.7f);
            var wave = Level.Waves[WaveNumber - 1];
            foreach (var group in wave.Groups)
            {
                if (!seenTypes.Contains(group.Type))
                {
                    seenTypes.Add(group.Type);
                    IntroduceEnemy(group.Type);
                }
                activeSpawners++;
                StartCoroutine(RunSpawnGroup(group, WaveNumber));
            }
        }

        IEnumerator RunSpawnGroup(SpawnGroup g, int wave)
        {
            if (g.Delay > 0f) yield return new WaitForSeconds(g.Delay);
            for (int i = 0; i < g.Count; i++)
            {
                if (State == GameState.Victory || State == GameState.Defeat) break;
                SpawnEnemy(g.Type, wave);
                if (i < g.Count - 1) yield return new WaitForSeconds(g.Interval);
            }
            activeSpawners--;
            if (activeSpawners <= 0)
            {
                activeSpawners = 0;
                if (WaveNumber < WaveCount && State != GameState.Defeat)
                {
                    WaveCountdown = true;
                    NextWaveTimer = Balance.WaveBreak;
                }
            }
        }

        Enemy SpawnEnemy(EnemyType type, int wave)
        {
            var e = Enemy.Create(type, world, Route, wave);
            Enemies.Add(e);
            if (e.IsBoss)
            {
                Boss = e;
                AudioManager.PlayMusic(Music.Boss);
                AudioManager.Play(Sfx.Roar);
            }
            Fx.Burst(e.Position, new Color(0.3f, 0.25f, 0.2f, 0.6f), 0.5f, 1.4f, 0.5f, Art.Circle, 16);
            return e;
        }

        void IntroduceEnemy(EnemyType type)
        {
            var def = EnemyData.Get(type);
            if (type == EnemyType.Grunt) return;
            if (def.IsBoss)
            {
                Announce("BOSS: " + def.Name, def.Intro, "Captain Brand", Palette.Bad, 9f);
                if (CamCtl != null) CamCtl.Shake(0.6f, 0.25f);
            }
            else Announce("New enemy: " + def.Name, def.Intro, "Captain Brand", Palette.Ember, 8f);
        }

        // ------------------------------------------------------------------ Events

        public void EnemyKilled(Enemy e)
        {
            Enemies.Remove(e);
            Kills++;
            int gold = e.Summoned ? 1 : e.Def.Gold;
            AddGold(gold);
            AddPopup(e.Position + Vector2.up * 0.4f, "+" + gold, Palette.Gold);
            if (e.LastTowerHit != null) e.LastTowerHit.AddKill();
            if (HeroUnit != null) HeroUnit.AddXp(gold * (e.LastHitByHero ? 2 : 1));

            // Coins fly to the gold counter (the gold is already counted; this is the feedback).
            int coins = e.IsBoss ? 12 : e.Summoned ? 1 : Mathf.Clamp(gold / 4, 1, 4);
            for (int i = 0; i < coins; i++)
                FlyToHud.Spawn(e.Position, Palette.Gold, Palette.Hex("#A87A12"), 0.32f, () => GoldIconGui, () => { GoldPulse = 1f; AudioManager.Play(Sfx.Coin, 0.35f); });

            // Ember Shard drop: one Dragonfire charge.
            int shards = e.IsBoss ? 2 : e.Summoned ? 0 : (Random.value < e.Def.ShardChance ? 1 : 0);
            for (int i = 0; i < shards; i++)
            {
                FlyToHud.Spawn(e.Position, Palette.Shard, Palette.Hex("#8A2E08"), 0.45f, () => PowerIconGui, () => AddPowerCharge(1));
                if (!shardTipShown && !Level.IsTutorial)
                {
                    shardTipShown = true;
                    Announce("Ember Shard!", "Orcs sometimes drop Ember Shards. Each one charges Dragonfire (up to 3). Drag the Dragonfire card onto a crowd of orcs.", "Captain Brand", Palette.Shard, 7f);
                }
            }

            if (e == Boss)
            {
                Boss = null;
                AudioManager.PlayMusic(BattleTrack);
                Announce(e.Def.Name + " has fallen!", "Finish off the rest of the wave!", "Captain Brand", Palette.Good, 5f);
                if (CamCtl != null) CamCtl.Shake(0.5f, 0.3f);
            }
        }

        public void EnemyReachedGate(Enemy e)
        {
            Enemies.Remove(e);
            if (e == Boss) { Boss = null; AudioManager.PlayMusic(BattleTrack); }
            if (State != GameState.Playing) return;
            Lives = Mathf.Max(0, Lives - e.Def.LifeCost);
            AddPopup(Level.Gate + Vector2.up * 1.5f, "-" + e.Def.LifeCost + (e.Def.LifeCost == 1 ? " life" : " lives"), Palette.Bad);
            if (gateFlash != null) gateFlash.color = new Color(1f, 0.15f, 0.1f, 0.8f);
            if (CamCtl != null) CamCtl.Shake(0.25f, 0.15f);
            AudioManager.Play(Sfx.LifeLost);
            if (Lives <= 0) Lose();
        }

        public void OnHeroDied()
        {
            if (heroTipShown) return;
            heroTipShown = true;
            Announce(HeroUnit.Name + " is down", "Heroes return at the gate when the gold clock fills (12s). Pull your hero back before their health runs out.", "Captain Brand", Palette.Bad, 6f);
        }

        public void OnHeroLevelUp(Hero h)
        {
            // The HUD shows a level-up panel (what changed + quick-learn buttons) while points are unspent.
            LevelUpLevel = h.Level;
            LevelUpPanelOpen = true;
        }

        /// <summary>Level-up panel next to the hero card. Stays open until the points are spent or dismissed.</summary>
        public bool LevelUpPanelOpen { get; private set; }
        public int LevelUpLevel { get; private set; }
        public void DismissLevelUp() { LevelUpPanelOpen = false; }

        public void OnHeroSlam() { SlamUsed = true; }

        public void OnInterrupt(Enemy e)
        {
            InterruptCount++;
            AddPopup(e.Position + Vector2.up * 0.9f, "INTERRUPTED! +25% damage taken", Palette.Interrupt);
            if (CamCtl != null) CamCtl.Shake(0.25f, 0.15f);
            if (interruptTipShown) return;
            interruptTipShown = true;
            Announce("Interrupted!", "Stuns cancel wind-ups and Stagger the enemy: it takes +25% damage for 3 seconds.", "Captain Brand", Palette.Interrupt, 6f);
        }

        public void OnVentWarning(EmberVent v)
        {
            if (ventTipShown) return;
            ventTipShown = true;
            if (Marsh) Announce("Marsh gas about to burst!", "The green flame burns the dead AND " + HeroUnit.Name + ", and reveals Wraiths. Hold them on it, then step clear.", "Captain Brand", Palette.Undead, 6f);
            else Announce("Ember Vent about to erupt!", "Its fire burns orcs AND " + HeroUnit.Name + ". Hold orcs on it, then step clear.", "Captain Brand", Palette.Ember, 6f);
        }

        public void OnRockslideTriggered()
        {
            if (rockTipShown) return;
            rockTipShown = true;
            if (Marsh) Announce("Deadfall!", "Rolling logs crush and stun everything in the lane, including " + HeroUnit.Name + ". More logs are ready in 40 seconds.", "Captain Brand", Palette.Ember, 6f);
            else Announce("Rockslide!", "Boulders crush and stun everything in the lane, including " + HeroUnit.Name + ". It rebuilds in 40 seconds.", "Captain Brand", Palette.Ember, 6f);
        }

        public void OnAshStorm()
        {
            if (stormTipShown) return;
            stormTipShown = true;
            if (Marsh) Announce("Marsh Fog rolling in!", "Towers inside the fog lose 40% range until it passes. Spread your defenses out.", "Captain Brand", Palette.Ash, 7f);
            else Announce("Ash Storm rolling in!", "Towers inside the grey cloud lose 40% range until it passes. Spread your defenses out.", "Captain Brand", Palette.Ash, 7f);
        }

        public void AddGold(int amount) { Gold += amount; }

        public void AddPowerCharge(int n)
        {
            if (PowerCharges >= Balance.DragonfireMaxCharges)
            {
                AddGold(25);
                AddPopup(Level.Gate + Vector2.up * 2f, "Dragonfire full: +25 gold", Palette.Gold);
                return;
            }
            PowerCharges = Mathf.Min(Balance.DragonfireMaxCharges, PowerCharges + n);
            PowerPulse = 1f;
            AudioManager.Play(Sfx.Shard, 0.7f);
        }

        public void AddPopup(Vector2 world, string text, Color color)
        {
            Popups.Add(new Popup { World = new Vector3(world.x, world.y, 0f), Text = text, Color = color });
        }

        public void Announce(string title, string body, string speaker, Color accent, float duration)
        {
            foreach (var a in Announcements) if (a.Title == title) return;
            Announcements.Add(new Announcement { Title = title, Body = body, Speaker = speaker, Accent = accent, Duration = duration });
            if (Announcements.Count > 3) Announcements.RemoveAt(1);
        }

        // ------------------------------------------------------------------ Queries

        public List<Enemy> EnemiesInRadius(Vector2 p, float r)
        {
            var list = new List<Enemy>();
            float r2 = r * r;
            foreach (var e in Enemies)
                if (e != null && e.Alive && (e.Position - p).sqrMagnitude <= r2) list.Add(e);
            return list;
        }

        public Enemy ClosestEnemy(Vector2 p, float r, Enemy exclude)
        {
            Enemy best = null;
            float bestD = r * r;
            foreach (var e in Enemies)
            {
                if (e == null || !e.Alive || e == exclude || e.IsHidden) continue;
                float d = (e.Position - p).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = e; }
            }
            return best;
        }

        /// <summary>War Drummer mechanic: towers inside any active drummer's aura attack 30% slower.</summary>
        public bool IsTowerSlowed(Vector2 towerPos)
        {
            float r2 = Balance.DrummerAuraRadius * Balance.DrummerAuraRadius;
            foreach (var e in Enemies)
                if (e != null && e.AuraActive && (e.Position - towerPos).sqrMagnitude <= r2) return true;
            return false;
        }

        public bool InAshStorm(Vector2 p) => Storm != null && Storm.Contains(p);

        // ------------------------------------------------------------------ Placement (Bloons style)

        public void BeginPlacement(TowerType type, bool dragging)
        {
            if (State != GameState.Playing) return;
            ClearSelection();
            Placing = true;
            PlacingPower = false;
            PlacingType = type;
            placeDrag = dragging;
            placeStartMouse = GameInput.MousePosition;
            AudioManager.Play(Sfx.Click, 0.5f);
        }

        public void BeginPowerPlacement(bool dragging)
        {
            if (State != GameState.Playing) return;
            if (PowerCharges <= 0)
            {
                AudioManager.Play(Sfx.Error, 0.6f);
                Announce("No Dragonfire charges", "Defeated orcs drop Ember Shards that recharge Dragonfire.", "", Palette.Dim, 4f);
                return;
            }
            ClearSelection();
            Placing = true;
            PlacingPower = true;
            placeDrag = dragging;
            placeStartMouse = GameInput.MousePosition;
            AudioManager.Play(Sfx.Click, 0.5f);
        }

        public void CancelPlacement()
        {
            Placing = false;
            PlacingPower = false;
            if (ghost != null) ghost.gameObject.SetActive(false);
        }

        void UpdatePlacement(Vector2 w, bool overUI)
        {
            string reason = "";
            bool valid;
            float radius;
            if (PlacingPower)
            {
                valid = WalkBounds.Contains(w);
                if (!valid) reason = "Out of bounds";
                radius = Balance.DragonfireRadius;
            }
            else
            {
                var def = TowerData.Get(PlacingType);
                valid = CanBuildAt(w, out reason);
                if (valid && Gold < def.Cost) { valid = false; reason = "Not enough gold"; }
                radius = def.Range;
            }
            PlacementValid = valid;
            PlacementReason = reason;

            ghost.gameObject.SetActive(!overUI);
            ghost.position = new Vector3(w.x, w.y, 0f);
            Color c = valid ? new Color(0.5f, 1f, 0.5f, 1f) : new Color(1f, 0.35f, 0.3f, 1f);
            ghostRange.transform.localScale = Vector3.one * radius * 2f;
            ghostRange.color = new Color(c.r, c.g, c.b, 0.7f);
            ghostBase.enabled = ghostRoof.enabled = !PlacingPower;
            if (PlacingPower) ghostRange.color = valid ? new Color(1f, 0.55f, 0.15f, 0.9f) : new Color(1f, 0.35f, 0.3f, 0.7f);
            else
            {
                ghostBase.color = new Color(c.r, c.g, c.b, 0.55f);
                ghostRoof.color = TowerData.Get(PlacingType).Roof;
            }
        }

        bool TryPlace(Vector2 w)
        {
            if (!PlacementValid)
            {
                AudioManager.Play(Sfx.Error, 0.6f);
                if (!string.IsNullOrEmpty(PlacementReason)) AddPopup(w + Vector2.up * 0.5f, PlacementReason, Palette.Bad);
                return false;
            }
            if (PlacingPower)
            {
                PowerCharges--;
                PowerUsed = true;
                DragonfireStrike.Cast(w);
            }
            else
            {
                var def = TowerData.Get(PlacingType);
                Gold -= def.Cost;
                var site = BuildPlot.Create(world, w);
                site.Building = true;
                Plots.Add(site);
                TowerConstruction.Begin(site, PlacingType);
                AudioManager.Play(Sfx.Place, 0.6f);
            }
            CancelPlacement();
            return true;
        }

        /// <summary>Called by TowerConstruction when the hammer strikes: the tower appears.</summary>
        public Tower FinishBuild(BuildPlot plot, TowerType type)
        {
            if (plot == null) return null;
            plot.Building = false;
            var tower = Tower.Create(type, plot, world);
            plot.Tower = tower;
            Towers.Add(tower);
            Fx.Burst(plot.Position, new Color(1f, 0.85f, 0.6f, 0.8f), 0.8f, 2.4f, 0.35f);
            Fx.Burst(plot.Position, new Color(0.5f, 0.45f, 0.4f, 0.6f), 1f, 2f, 0.6f, Art.Circle, 16);
            if (CamCtl != null) CamCtl.Shake(0.12f, 0.06f);
            return tower;
        }

        public bool TryUpgrade(Tower tower, int branch)
        {
            string reason;
            if (tower == null || State != GameState.Playing || !tower.CanUpgrade(branch, out reason)) return false;
            int cost = tower.NextCost(branch);
            if (Gold < cost) { AudioManager.Play(Sfx.Error, 0.6f); return false; }
            Gold -= cost;
            tower.ApplyUpgrade(branch, cost);
            UpgradesBought++;
            return true;
        }

        public void Sell(Tower tower)
        {
            if (tower == null || State != GameState.Playing) return;
            int refund = tower.SellValue;
            AddGold(refund);
            AddPopup(tower.Position + Vector2.up * 0.6f, "+" + refund, Palette.Gold);
            AudioManager.Play(Sfx.Sell);
            Towers.Remove(tower);
            Fx.Burst(tower.Position, new Color(0.5f, 0.45f, 0.4f, 0.7f), 1f, 2.2f, 0.5f, Art.Circle, 16);
            ClearSelection();
            if (tower.Plot != null)
            {
                Plots.Remove(tower.Plot);
                Destroy(tower.Plot.gameObject);
            }
            Destroy(tower.gameObject);
        }

        // ------------------------------------------------------------------ Selection

        public void ClearSelection()
        {
            if (SelectedTower != null) SelectedTower.SetSelected(false);
            if (HeroUnit != null) HeroUnit.SetSelected(false);
            SelectedTower = null;
            HeroSelected = false;
        }

        public void SelectTower(Tower tower)
        {
            ClearSelection();
            SelectedTower = tower;
            tower.SetSelected(true);
            AudioManager.Play(Sfx.Click, 0.4f);
        }

        public void SelectHero()
        {
            ClearSelection();
            if (HeroUnit == null) return;
            HeroSelected = true;
            HeroUnit.SetSelected(true);
            AudioManager.Play(Sfx.Click, 0.4f);
        }

        Tower TowerAt(Vector2 p)
        {
            foreach (var t in Towers)
                if (t != null && Vector2.Distance(t.Position, p) <= 0.8f) return t;
            return null;
        }

        static Vector2 ClampToWalk(Vector2 p)
        {
            return new Vector2(Mathf.Clamp(p.x, WalkBounds.xMin, WalkBounds.xMax), Mathf.Clamp(p.y, WalkBounds.yMin, WalkBounds.yMax));
        }

        // ------------------------------------------------------------------ Input

        void HandleInput()
        {
            if (GameInput.KeyPressed(GKey.M)) AudioManager.Muted = !AudioManager.Muted;

            if (GameInput.KeyPressed(GKey.Escape))
            {
                if (State == GameState.Paused) Resume();
                else if (State == GameState.Playing)
                {
                    if (Placing) CancelPlacement();
                    else if (HasSelection) ClearSelection();
                    else Pause();
                }
            }
            if (State != GameState.Playing) { CancelPlacement(); return; }

            if (GameInput.KeyPressed(GKey.Space)) StartNextWave(true);
            if (GameInput.KeyPressed(GKey.F)) ToggleSpeed();
            if (GameInput.KeyPressed(GKey.Q) && HeroUnit != null) HeroUnit.UseAbility();
            if (GameInput.KeyPressed(GKey.H) && HeroUnit != null)
            {
                CamCtl.Focus(HeroUnit.Dead ? HeroUnit.RespawnPoint : HeroUnit.Position);
                SelectHero();
            }
            if (GameInput.KeyPressed(GKey.Digit1)) BeginPlacement(TowerType.Archer, false);
            if (GameInput.KeyPressed(GKey.Digit2)) BeginPlacement(TowerType.Catapult, false);
            if (GameInput.KeyPressed(GKey.Digit3)) BeginPlacement(TowerType.Mage, false);
            if (GameInput.KeyPressed(GKey.Digit4) || GameInput.KeyPressed(GKey.E)) BeginPowerPlacement(false);
            if (GameInput.KeyPressed(GKey.R) && Cliff != null) Cliff.Trigger();

            // Debug keys for demos and testing
            if (GameInput.KeyPressed(GKey.F9))
            {
                AddGold(500);
                AddPowerCharge(1);
                AddPopup(Level.Gate + Vector2.up * 2.5f, "+500 gold (debug)", Palette.Gold);
            }
            if (GameInput.KeyPressed(GKey.F10)) DebugSkipToBoss();

            if (Cam == null) return;
            Vector3 w3 = Cam.ScreenToWorldPoint(GameInput.MousePosition);
            Vector2 w = new Vector2(w3.x, w3.y);
            bool overUI = UI.PointerOverUI;

            if (Placing)
            {
                UpdatePlacement(w, overUI);
                if (placeDrag)
                {
                    // Dragged from the shop: release on the map to place, release on the HUD to cancel.
                    // A quick click on a shop card (no drag) switches to click-to-place.
                    if (GameInput.LeftReleased)
                    {
                        if (overUI)
                        {
                            if ((GameInput.MousePosition - placeStartMouse).magnitude < 12f * UI.Scale) placeDrag = false;
                            else CancelPlacement();
                        }
                        else TryPlace(w);
                    }
                }
                else
                {
                    if (GameInput.LeftPressed && !UI.Busy) TryPlace(w);
                    if (GameInput.RightPressed) CancelPlacement();
                }
                return;
            }

            if (UI.Busy) return;

            if (GameInput.LeftPressed)
            {
                if (HeroUnit != null && HeroUnit.HitTest(w)) { SelectHero(); return; }
                var tower = TowerAt(w);
                if (tower != null) { SelectTower(tower); return; }
                if (Cliff != null && Cliff.HitTest(w)) { Cliff.Trigger(); return; }
                if (HeroSelected && HeroUnit != null && !HeroUnit.Dead)
                {
                    HeroUnit.MoveTo(ClampToWalk(w));
                    return;
                }
                ClearSelection();
            }

            if (GameInput.RightPressed && HeroUnit != null && !HeroUnit.Dead)
                HeroUnit.MoveTo(ClampToWalk(w));
        }

        // ------------------------------------------------------------------ Flow

        public void Pause()
        {
            if (State != GameState.Playing) return;
            CancelPlacement();
            State = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = FastForward ? 2f : 1f;
        }

        public void ToggleSpeed()
        {
            FastForward = !FastForward;
            if (State == GameState.Playing) Time.timeScale = FastForward ? 2f : 1f;
        }

        void Win()
        {
            if (State != GameState.Playing) return;
            State = GameState.Victory;
            ClearSelection();
            CancelPlacement();
            float frac = (float)Lives / MaxLives;
            StarsEarned = frac >= 0.9f ? 3 : frac >= 0.5f ? 2 : 1;
            GameSession.SaveStars(Level.Id, GameSession.CurrentDifficulty, StarsEarned);
            AudioManager.Play(Sfx.Victory);
            AudioManager.PlayMusic(Music.Title);
            Time.timeScale = 1f;
        }

        void Lose()
        {
            if (State == GameState.Defeat) return;
            State = GameState.Defeat;
            ClearSelection();
            CancelPlacement();
            StarsEarned = 0;
            AudioManager.Play(Sfx.Defeat);
            AudioManager.PlayMusic(Music.Title);
            Time.timeScale = 0f;
        }

        public void Restart() { SceneRouter.Go(SceneNames.Level1); }
        public void ToLevelSelect() { SceneRouter.Go(SceneNames.LevelSelect); }
        public void ToTitle() { SceneRouter.Go(SceneNames.Title); }

        /// <summary>F10: clear the field and jump straight to the final boss wave (wave 30 on the Ashen Pass).</summary>
        void DebugSkipToBoss()
        {
            StopAllCoroutines();
            activeSpawners = 0;
            foreach (var e in Enemies.ToArray())
                if (e != null) e.Despawn();
            Enemies.Clear();
            Boss = null;
            AudioManager.PlayMusic(BattleTrack);
            WaveNumber = Mathf.Max(0, Level.BossWave - 1);
            WaveCountdown = true;
            NextWaveTimer = 5f;
            AddGold(1500);
            PowerCharges = Balance.DragonfireMaxCharges;
            if (HeroUnit != null) HeroUnit.AddXp(800);
            if (Tutorial != null) Tutorial.SkipAll();
            Announce("Debug: skipping to the boss", "Wave " + (WaveNumber + 1) + " is next. +1500 gold, full Dragonfire, hero XP.", "", Palette.Dim, 5f);
        }
    }
}

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
    /// Level 1: The Ashen Pass. Builds the map, runs waves, tracks gold and lives,
    /// handles player input in the world and owns the win/lose flow.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager I { get; private set; }

        public PathRoute Route { get; private set; }
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Tower> Towers = new List<Tower>();
        public readonly List<BuildPlot> Plots = new List<BuildPlot>();
        public readonly List<Popup> Popups = new List<Popup>();
        public readonly List<Announcement> Announcements = new List<Announcement>();

        public Hero HeroUnit { get; private set; }
        public EmberVent Vent { get; private set; }
        public Camera Cam { get; private set; }
        public CameraController CamCtl { get; private set; }
        public BossGorruk Boss { get; private set; }

        public int Gold { get; private set; }
        public int Lives { get; private set; }
        public int MaxLives { get; private set; }
        public int WaveNumber { get; private set; }
        public int WaveCount => Level1Data.Waves.Length;
        public float NextWaveTimer { get; private set; }
        public bool WaveCountdown { get; private set; }
        public bool Spawning => activeSpawners > 0;
        public GameState State { get; private set; }
        public bool FastForward { get; private set; }
        public float ElapsedTime { get; private set; }
        public int Kills { get; private set; }
        public int StarsEarned { get; private set; }

        public BuildPlot SelectedPlot { get; private set; }
        public Tower SelectedTower { get; private set; }
        public bool HeroSelected { get; private set; }
        public bool HasSelection => SelectedPlot != null || SelectedTower != null || HeroSelected;

        public int EarlyBonus => WaveNumber == 0 || !GameSession.EarlyCallBonus ? 0 : Mathf.CeilToInt(NextWaveTimer) * Balance.EarlyBonusPerSecond;
        public string NextWavePreview => WaveNumber < WaveCount ? Level1Data.Waves[WaveNumber].Summary() : "";

        public static readonly Rect MapBounds = new Rect(-20f, -12f, 40f, 24f);
        static readonly Rect WalkBounds = new Rect(-18f, -9.5f, 35f, 19f);

        int activeSpawners;
        bool ventTipShown, heroTipShown;
        Transform world;
        SpriteRenderer gateFlash;
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
            world = new GameObject("World").transform;
            Cam = Art.MakeCamera(Palette.GroundDark, 10.8f);
            CamCtl = Cam.gameObject.AddComponent<CameraController>();
            CamCtl.Init(Cam, MapBounds);

            Route = new PathRoute(Level1Data.Path);
            BuildMap();
            HeroUnit = Hero.Create(world, Level1Data.HeroStart, Level1Data.HeroRespawn);

            Gold = Balance.StartGold;
            MaxLives = Lives = GameSession.StartLives;
            WaveNumber = 0;
            WaveCountdown = true;
            NextWaveTimer = Balance.FirstWaveDelay;
            State = GameState.Playing;

            gameObject.AddComponent<HUD>();
            Announce(Level1Data.Name, "The Orc Warhost is coming down the mountain road, Commander. Click a stone plot to build towers, and right-click to move Sir Aldric into their path.", "Captain Brand", Palette.Ember, 9f);
        }

        // ------------------------------------------------------------------ Map

        void BuildMap()
        {
            Random.InitState(1107);
            Art.Make("ground", world, Art.Square, Palette.Ground, Vector2.zero, new Vector2(MapBounds.width + 4f, MapBounds.height + 4f), 0);
            for (int i = 0; i < 26; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19f, 19f), Random.Range(-11f, 11f));
                float s = Random.Range(3f, 7f);
                Art.Make("patch", world, Art.Glow, new Color(0.12f, 0.09f, 0.07f, 0.45f), p, new Vector2(s, s * 0.7f), 1);
            }
            // Embers glowing under the ash
            for (int i = 0; i < 14; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19f, 19f), Random.Range(-11f, 11f));
                if (Route.DistanceTo(p) < 2f) continue;
                Art.Make("emberPatch", world, Art.Glow, new Color(0.9f, 0.35f, 0.1f, 0.18f), p, Vector2.one * Random.Range(1.5f, 3f), 1);
            }

            // Road
            var pts = Level1Data.Path;
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                Vector2 dir = (b - a).normalized;
                Art.Segment("roadEdge", world, a - dir * 1f, b + dir * 1f, 2.1f, Palette.RoadEdge, 2);
            }
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                Vector2 dir = (b - a).normalized;
                Art.Segment("road", world, a - dir * 0.8f, b + dir * 0.8f, 1.7f, Palette.Road, 3);
            }
            // Ruts and pebbles along the road
            for (float d = 0f; d < Route.Length; d += 0.9f)
            {
                Vector2 p = Route.PositionAt(d) + Random.insideUnitCircle * 0.55f;
                Art.Make("pebble", world, Art.Circle, new Color(0.45f, 0.36f, 0.27f, 0.6f), p, Vector2.one * Random.Range(0.08f, 0.18f), 3);
            }

            // Rocks and boulders off the road
            for (int i = 0; i < 70; i++)
            {
                Vector2 p = new Vector2(Random.Range(-19.5f, 19.5f), Random.Range(-11.5f, 11.5f));
                if (Route.DistanceTo(p) < 1.6f || NearPlot(p, 1.3f) || Vector2.Distance(p, Level1Data.GatePosition) < 3f) continue;
                float s = Random.Range(0.25f, 0.9f);
                Art.Make("rockShadow", world, Art.Circle, new Color(0f, 0f, 0f, 0.3f), p + new Vector2(0.06f, -0.08f), new Vector2(s * 1.1f, s * 0.8f), 1);
                Art.Make("rock", world, Art.Circle, Color.Lerp(Palette.Rock, Palette.StoneDark, Random.value), p, new Vector2(s, s * 0.8f), 1);
            }

            // Mountain cave where the Warhost emerges
            Vector2 cave = pts[0];
            Art.Make("caveGlow", world, Art.Glow, new Color(0.8f, 0.2f, 0.1f, 0.5f), cave + Vector2.left * 1.2f, new Vector2(5f, 5f), 4);
            Art.Make("cave", world, Art.Circle, Palette.Hex("#120D0B"), cave + Vector2.left * 1.6f, new Vector2(3.4f, 3.4f), 5);

            // Plots
            foreach (var p in Level1Data.Plots) Plots.Add(BuildPlot.Create(world, p));

            // Hazard
            Vent = EmberVent.Create(world, Level1Data.VentPosition);

            // The Last Bastion (gate on the right edge)
            Vector2 g = Level1Data.GatePosition;
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

        bool NearPlot(Vector2 p, float r)
        {
            foreach (var plot in Level1Data.Plots)
                if (Vector2.Distance(plot, p) < r) return true;
            return false;
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

            HandleInput();

            if (gateFlash != null && gateFlash.color.a > 0f)
                gateFlash.color = new Color(1f, 0.15f, 0.1f, Mathf.Max(0f, gateFlash.color.a - udt * 1.5f));

            if (State != GameState.Playing) return;
            float dt = Time.deltaTime;
            ElapsedTime += dt;

            if (WaveCountdown && WaveNumber < WaveCount)
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

        public void StartNextWave(bool calledEarly)
        {
            if (State != GameState.Playing || !WaveCountdown || WaveNumber >= WaveCount) return;

            if (calledEarly && EarlyBonus > 0)
            {
                int bonus = EarlyBonus;
                AddGold(bonus);
                AddPopup(Level1Data.GatePosition + Vector2.up * 2.5f, "+" + bonus + " gold (early call)", Palette.Gold);
            }
            if (WaveNumber > 0) AddGold(Balance.WaveBounty(WaveNumber));

            WaveNumber++;
            WaveCountdown = false;
            var wave = Level1Data.Waves[WaveNumber - 1];
            foreach (var group in wave.Groups)
            {
                if (!seenTypes.Contains(group.Type))
                {
                    seenTypes.Add(group.Type);
                    IntroduceEnemy(group.Type);
                }
                activeSpawners++;
                StartCoroutine(RunSpawnGroup(group));
            }
        }

        IEnumerator RunSpawnGroup(SpawnGroup g)
        {
            if (g.Delay > 0f) yield return new WaitForSeconds(g.Delay);
            for (int i = 0; i < g.Count; i++)
            {
                if (State == GameState.Victory || State == GameState.Defeat) break;
                SpawnEnemy(g.Type);
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

        Enemy SpawnEnemy(EnemyType type)
        {
            var e = Enemy.Create(type, world, Route);
            Enemies.Add(e);
            if (type == EnemyType.Gorruk) Boss = e as BossGorruk;
            Fx.Burst(e.Position, new Color(0.3f, 0.25f, 0.2f, 0.6f), 0.5f, 1.4f, 0.5f, Art.Circle, 16);
            return e;
        }

        void IntroduceEnemy(EnemyType type)
        {
            var def = EnemyData.Get(type);
            switch (type)
            {
                case EnemyType.Grunt:
                    break;
                case EnemyType.Gorruk:
                    Announce("BOSS INCOMING: " + def.Name, def.Intro, "Captain Brand", Palette.Bad, 9f);
                    if (CamCtl != null) CamCtl.Shake(0.6f, 0.25f);
                    break;
                default:
                    Announce("New enemy: " + def.Name, def.Intro, "Captain Brand", Palette.Ember, 8f);
                    break;
            }
        }

        // ------------------------------------------------------------------ Events

        public void EnemyKilled(Enemy e)
        {
            Enemies.Remove(e);
            Kills++;
            AddGold(e.Def.Gold);
            AddPopup(e.Position + Vector2.up * 0.4f, "+" + e.Def.Gold, Palette.Gold);
            if (e == Boss)
            {
                Boss = null;
                Announce("Gorruk has fallen!", "The Warlord is down. Finish off the rest of the Warhost!", "Captain Brand", Palette.Good, 6f);
                if (CamCtl != null) CamCtl.Shake(0.5f, 0.3f);
            }
        }

        public void EnemyReachedGate(Enemy e)
        {
            Enemies.Remove(e);
            if (e == Boss) Boss = null;
            if (State != GameState.Playing) return;
            Lives = Mathf.Max(0, Lives - e.Def.LifeCost);
            AddPopup(Level1Data.GatePosition + Vector2.up * 1.5f, "-" + e.Def.LifeCost + (e.Def.LifeCost == 1 ? " life" : " lives"), Palette.Bad);
            if (gateFlash != null) gateFlash.color = new Color(1f, 0.15f, 0.1f, 0.8f);
            if (CamCtl != null) CamCtl.Shake(0.25f, 0.15f);
            if (Lives <= 0) Lose();
        }

        public void OnHeroDied()
        {
            if (heroTipShown) return;
            heroTipShown = true;
            Announce("Sir Aldric is down", "He'll be back at the gate in 12 seconds. Keep him out of the Ember Vent's fire, and pull him back when his health runs low.", "Captain Brand", Palette.Bad, 7f);
        }

        public void OnVentWarning(EmberVent v)
        {
            if (ventTipShown) return;
            ventTipShown = true;
            Announce("The Ember Vent is about to erupt!", "Its fire burns orcs and Sir Aldric alike. Hold the orcs on the vent with Sir Aldric, then pull him clear before it blows.", "Captain Brand", Palette.Ember, 8f);
        }

        public void AddGold(int amount) { Gold += amount; }

        public void AddPopup(Vector2 world, string text, Color color)
        {
            Popups.Add(new Popup { World = new Vector3(world.x, world.y, 0f), Text = text, Color = color });
        }

        public void Announce(string title, string body, string speaker, Color accent, float duration)
        {
            Announcements.Add(new Announcement { Title = title, Body = body, Speaker = speaker, Accent = accent, Duration = duration });
            if (Announcements.Count > 4) Announcements.RemoveAt(1);
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
                if (e == null || !e.Alive || e == exclude) continue;
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

        // ------------------------------------------------------------------ Building

        public bool TryBuild(BuildPlot plot, TowerType type)
        {
            if (plot == null || plot.Tower != null || State != GameState.Playing) return false;
            var def = TowerData.Get(type);
            if (Gold < def.Cost) return false;
            Gold -= def.Cost;
            var tower = Tower.Create(type, plot, world);
            plot.Tower = tower;
            Towers.Add(tower);
            Fx.Burst(plot.Position, new Color(1f, 0.85f, 0.6f, 0.8f), 0.8f, 2.4f, 0.35f);
            Fx.Burst(plot.Position, new Color(0.5f, 0.45f, 0.4f, 0.6f), 1f, 2f, 0.6f, Art.Circle, 16);
            SelectTower(tower);
            return true;
        }

        public bool TryUpgrade(Tower tower, int branch)
        {
            string reason;
            if (tower == null || State != GameState.Playing || !tower.CanUpgrade(branch, out reason)) return false;
            int cost = tower.NextCost(branch);
            if (Gold < cost) return false;
            Gold -= cost;
            tower.ApplyUpgrade(branch, cost);
            return true;
        }

        public void Sell(Tower tower)
        {
            if (tower == null || State != GameState.Playing) return;
            int refund = tower.SellValue;
            AddGold(refund);
            AddPopup(tower.Position + Vector2.up * 0.6f, "+" + refund, Palette.Gold);
            if (tower.Plot != null) tower.Plot.Tower = null;
            Towers.Remove(tower);
            Fx.Burst(tower.Position, new Color(0.5f, 0.45f, 0.4f, 0.7f), 1f, 2.2f, 0.5f, Art.Circle, 16);
            ClearSelection();
            Destroy(tower.gameObject);
        }

        // ------------------------------------------------------------------ Selection

        public void ClearSelection()
        {
            if (SelectedPlot != null) SelectedPlot.SetSelected(false);
            if (SelectedTower != null) SelectedTower.SetSelected(false);
            if (HeroUnit != null) HeroUnit.SetSelected(false);
            SelectedPlot = null;
            SelectedTower = null;
            HeroSelected = false;
        }

        public void SelectPlot(BuildPlot plot)
        {
            ClearSelection();
            SelectedPlot = plot;
            plot.SetSelected(true);
        }

        public void SelectTower(Tower tower)
        {
            ClearSelection();
            SelectedTower = tower;
            tower.SetSelected(true);
        }

        public void SelectHero()
        {
            ClearSelection();
            if (HeroUnit == null || HeroUnit.Dead) return;
            HeroSelected = true;
            HeroUnit.SetSelected(true);
        }

        BuildPlot PlotAt(Vector2 p)
        {
            foreach (var plot in Plots)
                if (Vector2.Distance(plot.Position, p) <= 0.8f) return plot;
            return null;
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
            if (GameInput.KeyPressed(GKey.Escape))
            {
                if (State == GameState.Paused) Resume();
                else if (State == GameState.Playing)
                {
                    if (HasSelection) ClearSelection();
                    else Pause();
                }
            }
            if (State != GameState.Playing) return;

            if (GameInput.KeyPressed(GKey.Space)) StartNextWave(true);
            if (GameInput.KeyPressed(GKey.F)) ToggleSpeed();
            if (GameInput.KeyPressed(GKey.Q) && HeroUnit != null) HeroUnit.UseAbility();
            if (GameInput.KeyPressed(GKey.H) && HeroUnit != null && !HeroUnit.Dead)
            {
                CamCtl.Focus(HeroUnit.Position);
                SelectHero();
            }
            if (SelectedPlot != null)
            {
                if (GameInput.KeyPressed(GKey.Digit1)) TryBuild(SelectedPlot, TowerType.Archer);
                else if (GameInput.KeyPressed(GKey.Digit2)) TryBuild(SelectedPlot, TowerType.Catapult);
                else if (GameInput.KeyPressed(GKey.Digit3)) TryBuild(SelectedPlot, TowerType.Mage);
            }

            // Debug keys for demos and testing
            if (GameInput.KeyPressed(GKey.F9))
            {
                AddGold(500);
                AddPopup(Level1Data.GatePosition + Vector2.up * 2.5f, "+500 gold (debug)", Palette.Gold);
            }
            if (GameInput.KeyPressed(GKey.F10)) DebugSkipToBoss();

            if (UI.Busy || Cam == null) return;
            Vector3 w3 = Cam.ScreenToWorldPoint(GameInput.MousePosition);
            Vector2 w = new Vector2(w3.x, w3.y);

            if (GameInput.LeftPressed)
            {
                if (HeroUnit != null && HeroUnit.HitTest(w)) { SelectHero(); return; }
                var tower = TowerAt(w);
                if (tower != null) { SelectTower(tower); return; }
                var plot = PlotAt(w);
                if (plot != null && plot.Tower == null) { SelectPlot(plot); return; }
                if (HeroSelected && HeroUnit != null)
                {
                    HeroUnit.MoveTo(ClampToWalk(w));
                    ClearSelection();
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
            float frac = (float)Lives / MaxLives;
            StarsEarned = frac >= 0.9f ? 3 : frac >= 0.5f ? 2 : 1;
            GameSession.SaveStars(1, GameSession.CurrentDifficulty, StarsEarned);
            Time.timeScale = 1f;
        }

        void Lose()
        {
            if (State == GameState.Defeat) return;
            State = GameState.Defeat;
            ClearSelection();
            StarsEarned = 0;
            Time.timeScale = 0f;
        }

        public void Restart() { SceneRouter.Go(SceneNames.Level1); }
        public void ToLevelSelect() { SceneRouter.Go(SceneNames.LevelSelect); }
        public void ToTitle() { SceneRouter.Go(SceneNames.Title); }

        void DebugSkipToBoss()
        {
            StopAllCoroutines();
            activeSpawners = 0;
            foreach (var e in Enemies.ToArray())
                if (e != null) e.Despawn();
            Enemies.Clear();
            Boss = null;
            WaveNumber = WaveCount - 1;
            WaveCountdown = true;
            NextWaveTimer = 5f;
            AddGold(800);
            Announce("Debug: skipping to the boss", "Wave " + WaveCount + " starts in 5 seconds. +800 gold to build a defense.", "", Palette.Dim, 5f);
        }
    }
}

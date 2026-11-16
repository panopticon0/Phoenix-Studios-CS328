using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Training Grounds: a guided tutorial level. Shows one objective at a time with an arrow to the
    /// HUD element or a glowing marker on the map, and advances when the player does it.
    /// Waves only start when the player presses Start Wave.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public enum Focus { None, ShopArcher, WaveButton, HeroSpot, SlamButton, Tower, PowerCard, Boss }

        class StepDef
        {
            public string Title, Body;
            public Focus Focus;
            public bool HasMarker;
            public Vector2 Marker;
            public System.Func<bool> Done;
            public System.Action OnEnter;
        }

        public bool Ready => steps != null;
        public int Step { get; private set; }
        public int StepCount => steps.Length;
        public string Title => steps[Step].Title;
        public string Body => steps[Step].Body;
        public Focus Target => steps[Step].Focus;
        public bool Finished => Step >= steps.Length - 1;
        public bool AllowWaveStart => Step >= 1;
        public bool HasMarker => steps[Step].HasMarker;
        public Vector2 Marker => steps[Step].Marker;

        LevelManager lm;
        StepDef[] steps;
        SpriteRenderer markerRing, markerArrow;
        float t, justAdvanced;

        void Start()
        {
            lm = GetComponent<LevelManager>();
            var L = lm.Level;
            steps = new[]
            {
                new StepDef { Title = "Build your first tower", Focus = Focus.ShopArcher, HasMarker = true, Marker = L.TutorialTowerSpot,
                    Body = "Drag the ARCHER TOWER card from the shop on the right and drop it on the glowing circle beside the road. A green ring means you can build there.",
                    Done = () => lm.Plots.Count > 0 },
                new StepDef { Title = "Start the first wave", Focus = Focus.WaveButton,
                    Body = "Press START WAVE at the bottom of the shop (or Space). In training, waves only start when you say so.",
                    Done = () => lm.WaveNumber >= 1 },
                new StepDef { Title = "Move your hero", Focus = Focus.HeroSpot, HasMarker = true, Marker = L.TutorialHeroSpot,
                    Body = "Sir Aldric is the knight with the golden halo. RIGHT-CLICK the glowing spot on the road to send him there. He blocks up to 3 orcs and fights them.",
                    Done = () => lm.HeroUnit != null && !lm.HeroUnit.Moving && Vector2.Distance(lm.HeroUnit.Position, L.TutorialHeroSpot) < 1.4f },
                new StepDef { Title = "Shield Slam", Focus = Focus.SlamButton,
                    Body = "When orcs reach Sir Aldric, press Q (or the button on his card) to Shield Slam: damage plus a stun. The gold ring around him pulses when it's ready.",
                    Done = () => lm.SlamUsed },
                new StepDef { Title = "Upgrade a tower", Focus = Focus.Tower,
                    Body = "Click your Archer Tower. Its upgrade panel opens on the side of the screen. Buy any upgrade (here's 150 gold).",
                    OnEnter = () => { lm.AddGold(150); lm.AddPopup(lm.Level.Gate + Vector2.up * 2f, "+150 gold (training)", Palette.Gold); },
                    Done = () => lm.UpgradesBought > 0 },
                new StepDef { Title = "Dragonfire power-up", Focus = Focus.PowerCard,
                    Body = "Orcs drop glowing Ember Shards that charge DRAGONFIRE. Drag the Dragonfire card onto a group of orcs to burn them.",
                    OnEnter = () => { if (lm.PowerCharges <= 0) lm.AddPowerCharge(1); },
                    Done = () => lm.PowerUsed },
                new StepDef { Title = "Invisible enemies", Focus = Focus.Tower,
                    Body = "Goblin Sneaks (wave 4) are invisible to towers. Give a tower sight: Archer Utility 'Eagle Eye' or Mage Utility 'True Sight'. Sir Aldric can see them up close.",
                    OnEnter = () => { lm.AddGold(100); },
                    Done = () => AnyTowerSees() || lm.WaveNumber >= 5 },
                new StepDef { Title = "Interrupt the boss", Focus = Focus.Boss,
                    Body = "Warchief Brakka (last wave) stops to wind up a Rally Horn that heals his orcs. Stun him DURING the wind-up with Shield Slam to INTERRUPT it.",
                    Done = () => lm.InterruptCount > 0 || (lm.WaveNumber >= lm.WaveCount && lm.Boss == null && !lm.Spawning) },
                new StepDef { Title = "Training complete!", Focus = Focus.None,
                    Body = "Hold the line until the last orc falls. Then take on the Ashen Pass!",
                    Done = () => false },
            };

            markerRing = Art.Make("tutorialMarker", null, Art.Ring, Palette.HeroGlow, Vector2.zero, Vector2.one * 1.6f, 33);
            markerArrow = Art.Make("tutorialArrow", null, Art.Triangle, Palette.HeroGlow, Vector2.zero, new Vector2(0.6f, 0.7f), 34, 180f);
            UpdateMarker();
            lm.Announce("Welcome to the Training Grounds", "Follow the objectives at the top of the screen.", "Captain Brand", Palette.Ember, 6f);
        }

        bool AnyTowerSees()
        {
            foreach (var t in lm.Towers) if (t != null && t.SeesInvisible) return true;
            return false;
        }

        public void SkipAll()
        {
            if (steps == null) return;
            Step = steps.Length - 1;
            UpdateMarker();
        }

        void Update()
        {
            if (steps == null || lm == null) return;
            t += Time.unscaledDeltaTime;
            if (justAdvanced > 0f) justAdvanced -= Time.unscaledDeltaTime;
            if (!Finished && steps[Step].Done())
            {
                Step++;
                justAdvanced = 1f;
                AudioManager.Play(Sfx.Upgrade, 0.8f);
                if (steps[Step].OnEnter != null) steps[Step].OnEnter();
                UpdateMarker();
            }
            if (markerRing.enabled)
            {
                markerRing.transform.localScale = Vector3.one * (1.5f + 0.2f * Mathf.Sin(t * 5f));
                markerArrow.transform.position = new Vector3(Marker.x, Marker.y + 1.3f + 0.25f * Mathf.Sin(t * 6f), 0f);
            }
        }

        public float FlashAmount => Mathf.Clamp01(justAdvanced);

        void UpdateMarker()
        {
            bool show = HasMarker;
            markerRing.enabled = markerArrow.enabled = show;
            if (show) markerRing.transform.position = new Vector3(Marker.x, Marker.y, 0f);
        }

        void OnDestroy()
        {
            if (markerRing != null) Destroy(markerRing.gameObject);
            if (markerArrow != null) Destroy(markerArrow.gameObject);
        }
    }
}

using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Level select: Training Grounds tutorial, the three campaign battles, difficulty and saved stars.
    /// Pressing Play on a campaign level opens the hero select (the tutorial always uses Sir Aldric).
    /// </summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        struct LevelCard
        {
            public int Id;
            public string Label, Name, Race, Setting, Boss;
            public Color Accent;
            public bool Playable;
        }

        static readonly LevelCard[] Cards =
        {
            new LevelCard { Id = Levels.TutorialId, Label = "TRAINING", Name = "Training Grounds", Race = "Tutorial", Setting = "Learn to build, upgrade, command Sir Aldric (the training hero), use Dragonfire and interrupt a boss. 6 short waves.", Boss = "Warchief Brakka", Accent = Palette.HeroGlow, Playable = true },
            new LevelCard { Id = Levels.AshenPassId, Label = "LEVEL 1", Name = "The Ashen Pass", Race = "Orc Warhost", Setting = "30 waves down a scorched mountain road. Ember Vent, Rockslide Cliff and Ash Storms.", Boss = "Gorruk Skullsplitter", Accent = Palette.Ember, Playable = true },
            new LevelCard { Id = Levels.WeepingMarshId, Label = "LEVEL 2", Name = "The Weeping Marsh", Race = "Undead Legion", Setting = "30 waves through fog-drowned graveyards. Wraiths, Banshees and Necromancers. Marsh Gas, the Deadfall tree and Marsh Fog.", Boss = "Vesperine, the Lich Queen", Accent = Palette.Undead, Playable = true },
            new LevelCard { Id = 3, Label = "LEVEL 3", Name = "Siege of the Bastion", Race = "Both armies, united", Setting = "The walls of the Last Bastion itself, attacked on two lanes at once.", Boss = "Malgrath the Hollow King", Accent = Palette.Magic, Playable = false },
        };

        int pendingLevel = -1;     // campaign level waiting on a hero pick
        int heroChoice;

        void Start()
        {
            heroChoice = GameSession.SelectedHero;
            Time.timeScale = 1f;
            Art.MakeCamera(Palette.Night, 5.4f);
            Art.Make("glowL", transform, Art.Glow, new Color(0.88f, 0.42f, 0.15f, 0.25f), new Vector2(-6f, -4f), new Vector2(14f, 8f), 0);
            Art.Make("glowR", transform, Art.Glow, new Color(0.45f, 0.3f, 0.6f, 0.2f), new Vector2(7f, 3f), new Vector2(14f, 8f), 0);
            AudioManager.PlayMusic(Music.Title);
        }

        void Update()
        {
            if (GameInput.KeyPressed(GKey.Escape))
            {
                if (pendingLevel >= 0) pendingLevel = -1;
                else SceneRouter.Go(SceneNames.Title);
            }
        }

        void OnGUI()
        {
            UI.Begin();
            UI.FrameStart();
            float W = UI.W, H = UI.H;
            bool prevEnabled = GUI.enabled;
            GUI.enabled = pendingLevel < 0;   // the hero select overlay owns input while open

            GUI.Label(new Rect(0f, 50f, W, 30f), "THE KINGDOM OF EMBERFALL", UI.S(24, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ember));
            UI.Shadowed(new Rect(0f, 82f, W, 80f), "Choose Your Battle", UI.S(60, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment), 4f);

            float total = Mathf.Min(1760f, W - 80f);
            float gap = 24f;
            float cw = (total - 3f * gap) / 4f;
            float x0 = (W - total) * 0.5f;
            for (int i = 0; i < Cards.Length; i++)
                DrawCard(Cards[i], new Rect(x0 + i * (cw + gap), 190f, cw, 580f));

            DrawDifficulty(W);

            if (UI.Button(new Rect(x0, H - 96f, 200f, 60f), "Back", BtnKind.Normal, 24)) SceneRouter.Go(SceneNames.Title);
            GUI.enabled = prevEnabled;
            if (pendingLevel >= 0) DrawHeroSelect(W, H);
            UI.FrameEnd();
        }

        void DrawCard(LevelCard c, Rect r)
        {
            UI.Panel(r, c.Playable);
            Rect band = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, 140f);
            UI.Rounded(band, new Color(c.Accent.r * 0.35f, c.Accent.g * 0.35f, c.Accent.b * 0.35f, 1f), 10f);
            float bx = band.x + 20f, by = band.y + 36f, bw = band.width - 40f;
            UI.Fill(new Rect(bx, by + 30f, bw * 0.35f, 12f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Fill(new Rect(bx + bw * 0.35f - 12f, by + 30f, 12f, 48f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Fill(new Rect(bx + bw * 0.35f - 12f, by + 66f, bw * 0.45f, 12f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Fill(new Rect(bx + bw * 0.8f - 12f, by + 20f, 12f, 58f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Rounded(new Rect(bx + bw * 0.8f - 2f, by - 4f, 46f, 46f), Palette.Stone, 6f);
            UI.Disc(new Rect(bx + bw * 0.8f + 12f, by + 10f, 18f, 18f), Palette.Gold);
            for (int i = 0; i < 4; i++) UI.Disc(new Rect(bx + 6f + i * 20f, by + 30f, 12f, 12f), c.Accent);

            float x = r.x + 20f, w = r.width - 40f;
            GUI.Label(new Rect(x, r.y + 152f, w, 24f), c.Label + "  ·  " + c.Race.ToUpper(), UI.S(16, TextAnchor.MiddleLeft, FontStyle.Bold, c.Accent));
            GUI.Label(new Rect(x, r.y + 180f, w, 80f), c.Name, UI.S(30, TextAnchor.UpperLeft, FontStyle.Bold, Palette.Parchment, true));
            GUI.Label(new Rect(x, r.y + 262f, w, 110f), c.Setting, UI.S(18, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            GUI.Label(new Rect(x, r.y + 374f, w, 44f), "<color=#A89A86>Boss:</color> " + c.Boss, UI.S(17, TextAnchor.UpperLeft, FontStyle.Bold, Palette.Parchment, true));

            if (c.Playable)
            {
                int stars = GameSession.GetStars(c.Id, GameSession.CurrentDifficulty);
                for (int i = 0; i < 3; i++)
                {
                    Rect s = new Rect(x + i * 42f, r.y + 424f, 32f, 32f);
                    UI.Disc(s, i < stars ? Palette.Gold : new Color(1f, 1f, 1f, 0.08f));
                    UI.Circle(s, i < stars ? Palette.Hex("#FFE9A0") : Palette.PanelBorder, 2f);
                }
                GUI.Label(new Rect(x + 134f, r.y + 424f, w - 134f, 32f), stars + "/3 on " + GameSession.CurrentDifficulty, UI.S(15, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
                if (UI.Button(new Rect(x, r.yMax - 92f, w, 68f), c.Id == Levels.TutorialId ? "Train" : "Play", BtnKind.Accent, 28))
                {
                    if (c.Id == Levels.TutorialId)
                    {
                        GameSession.SelectedLevel = c.Id;
                        SceneRouter.Go(SceneNames.Level1);
                    }
                    else pendingLevel = c.Id;
                }
            }
            else
            {
                Rect lockR = new Rect(x, r.yMax - 92f, w, 68f);
                UI.Rounded(lockR, new Color(0f, 0f, 0f, 0.35f), 10f);
                UI.Outline(lockR, Palette.PanelBorder, 2f, 10f);
                GUI.Label(lockR, "LOCKED\n<size=15><color=#A89A86>coming in the full release</color></size>", UI.S(21, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim));
            }
        }

        // ------------------------------------------------------------------ Hero select

        void DrawHeroSelect(float W, float H)
        {
            UI.Veil(0.7f);
            float w = Mathf.Min(1280f, W - 60f), h = 780f;
            Rect r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            UI.Panel(r, true);
            var level = Levels.Get(pendingLevel);
            GUI.Label(new Rect(r.x, r.y + 18f, w, 28f), level.Name.ToUpper(), UI.S(20, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ember));
            UI.Shadowed(new Rect(r.x, r.y + 46f, w, 60f), "Choose Your Hero", UI.S(46, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment), 3f);

            float gap = 28f;
            float cw = (w - 80f - gap) / 2f, ch = 540f;
            for (int i = 0; i < HeroData.All.Length; i++)
            {
                var d = HeroData.All[i];
                Rect c = new Rect(r.x + 40f + i * (cw + gap), r.y + 120f, cw, ch);
                bool on = heroChoice == d.Id;
                bool hover = c.Contains(Event.current.mousePosition);
                UI.Rounded(c, on ? new Color(d.Glow.r * 0.16f, d.Glow.g * 0.16f, d.Glow.b * 0.16f, 1f) : new Color(1f, 1f, 1f, hover ? 0.06f : 0.03f), 14f);
                UI.Outline(c, on ? d.Glow : Palette.PanelBorder, on ? 4f : 2f, 14f);

                Rect face = new Rect(c.x + 24f, c.y + 24f, 132f, 132f);
                if (on)
                {
                    float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    UI.Disc(new Rect(face.x - 8f * p, face.y - 8f * p, face.width + 16f * p, face.height + 16f * p), new Color(d.Glow.r, d.Glow.g, d.Glow.b, 0.25f));
                }
                HUD.DrawHeroFace(face, d, false);

                float tx = face.xMax + 22f, tw = c.xMax - tx - 20f;
                GUI.Label(new Rect(tx, c.y + 24f, tw, 40f), d.Name, UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment, true));
                GUI.Label(new Rect(tx, c.y + 66f, tw, 24f), d.Title.ToUpper() + "  ·  " + d.Role, UI.S(16, TextAnchor.MiddleLeft, FontStyle.Bold, d.Glow));
                GUI.Label(new Rect(tx, c.y + 96f, tw, 70f), d.Blurb, UI.S(16, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));

                float y = c.y + 178f, lx = c.x + 24f, lw = c.width - 48f;
                StatBar(new Rect(lx, y, lw, 24f), "Health", d.BaseHp / 420f, Mathf.RoundToInt(d.BaseHp).ToString());
                StatBar(new Rect(lx, y + 30f, lw, 24f), "Damage / sec", (d.Damage / d.AttackInterval) / 30f, (d.Damage / d.AttackInterval).ToString("0"));
                StatBar(new Rect(lx, y + 60f, lw, 24f), "Speed", d.Speed / 4.6f, d.Speed.ToString("0.0"));
                StatBar(new Rect(lx, y + 90f, lw, 24f), d.Ranged ? "Range" : "Blocks", d.Ranged ? d.Range / 5f : d.MaxBlock / 4f, d.Ranged ? d.Range.ToString("0.0") : d.MaxBlock + " enemies");
                StatBar(new Rect(lx, y + 120f, lw, 24f), "Reveals invisible", d.RevealRadius / 4.6f, d.RevealRadius.ToString("0.0") + " units");
                y += 160f;

                Rect ab = new Rect(lx, y, lw, 104f);
                UI.Rounded(ab, new Color(0f, 0f, 0f, 0.3f), 10f);
                GUI.Label(new Rect(ab.x + 14f, ab.y + 8f, ab.width - 28f, 26f), "<color=#A89A86>Q:</color> " + d.AbilityName + "   <size=15><color=#A89A86>" + d.AbilityCooldown.ToString("0") + "s cooldown</color></size>", UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Gold));
                GUI.Label(new Rect(ab.x + 14f, ab.y + 36f, ab.width - 28f, 64f), d.AbilityDesc, UI.S(16, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Parchment, true));
                y += 116f;

                GUI.Label(new Rect(lx, y, lw, 24f), "LEVEL-UP TREE", UI.S(14, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Dim));
                float bw = (lw - 16f) / 3f;
                for (int b = 0; b < 3; b++)
                {
                    Rect br = new Rect(lx + b * (bw + 8f), y + 26f, bw, 38f);
                    Color bc = Palette.Branch(b);
                    UI.Rounded(br, new Color(bc.r * 0.25f, bc.g * 0.25f, bc.b * 0.25f, 1f), 8f);
                    GUI.Label(br, d.BranchNames[b] + "\n<size=12><color=#A89A86>" + d.Tree[b, 2].Name + "</color></size>", UI.S(15, TextAnchor.MiddleCenter, FontStyle.Bold, bc));
                }

                if (GUI.Button(c, GUIContent.none, GUIStyle.none) && heroChoice != d.Id)
                {
                    heroChoice = d.Id;
                    AudioManager.Play(Sfx.Click);
                }
            }

            float by = r.yMax - 92f;
            if (UI.Button(new Rect(r.x + 40f, by, 200f, 64f), "Back", BtnKind.Normal, 24)) pendingLevel = -1;
            var chosen = HeroData.Get(heroChoice);
            if (UI.Button(new Rect(r.xMax - 40f - 380f, by, 380f, 64f), "Begin with " + chosen.ShortName, BtnKind.Accent, 26))
            {
                GameSession.SelectedHero = heroChoice;
                GameSession.SelectedLevel = pendingLevel;
                SceneRouter.Go(SceneNames.Level1);
            }
            GUI.Label(new Rect(r.x + 260f, by, w - 260f - 440f, 64f), "The Training Grounds always use Sir Aldric.", UI.S(15, TextAnchor.MiddleCenter, FontStyle.Italic, Palette.Dim, true));
        }

        static void StatBar(Rect r, string label, float frac, string value)
        {
            GUI.Label(new Rect(r.x, r.y, 170f, r.height), label, UI.S(15, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));
            Rect bar = new Rect(r.x + 176f, r.y + 7f, r.width - 176f - 110f, 10f);
            UI.Bar(bar, Mathf.Clamp01(frac), Palette.Gold, new Color(0f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(bar.xMax + 10f, r.y, 100f, r.height), value, UI.S(15, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
        }

        void DrawDifficulty(float W)
        {
            float y = 800f;
            GUI.Label(new Rect(0f, y, W, 30f), "DIFFICULTY", UI.S(20, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim));
            string[] names = { "Recruit", "Warden", "Veteran" };
            string[] notes = { "Orcs have 20% less health", "The intended challenge", "+25% orc health, 10 lives, no early-call gold" };
            float bw = 240f, gap = 16f;
            float x0 = (W - (3f * bw + 2f * gap)) * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                var d = (Difficulty)i;
                bool on = GameSession.CurrentDifficulty == d;
                Rect b = new Rect(x0 + i * (bw + gap), y + 38f, bw, 56f);
                if (UI.Button(b, names[i], on ? BtnKind.Accent : BtnKind.Ghost, 24)) GameSession.CurrentDifficulty = d;
            }
            int idx = (int)GameSession.CurrentDifficulty;
            GUI.Label(new Rect(0f, y + 102f, W, 30f), notes[idx], UI.S(20, TextAnchor.MiddleCenter, FontStyle.Italic, Palette.Muted));
        }
    }
}

using UnityEngine;

namespace LastBastion
{
    /// <summary>Level select: the three campaign battles, difficulty choice and saved stars.</summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        struct LevelCard
        {
            public int Number;
            public string Name, Race, Setting, Boss;
            public Color Accent;
            public bool Playable;
        }

        static readonly LevelCard[] Cards =
        {
            new LevelCard { Number = 1, Name = "The Ashen Pass", Race = "Orc Warhost", Setting = "A scorched mountain road on the eastern frontier. Brute force, armor and speed.", Boss = "Gorruk Skullsplitter", Accent = Palette.Ember, Playable = true },
            new LevelCard { Number = 2, Name = "The Weeping Marsh", Race = "Undead Legion", Setting = "Fog-drowned graveyards and sunken ruins in the west. Trickery and the risen dead.", Boss = "Vesperine, the Lich Queen", Accent = Palette.Undead, Playable = false },
            new LevelCard { Number = 3, Name = "Siege of the Bastion", Race = "Both armies, united", Setting = "The walls of the Last Bastion itself, attacked on two lanes at once.", Boss = "Malgrath the Hollow King", Accent = Palette.Magic, Playable = false },
        };

        void Start()
        {
            Time.timeScale = 1f;
            Art.MakeCamera(Palette.Night, 5.4f);
            Art.Make("glowL", transform, Art.Glow, new Color(0.88f, 0.42f, 0.15f, 0.25f), new Vector2(-6f, -4f), new Vector2(14f, 8f), 0);
            Art.Make("glowR", transform, Art.Glow, new Color(0.45f, 0.3f, 0.6f, 0.2f), new Vector2(7f, 3f), new Vector2(14f, 8f), 0);
        }

        void Update()
        {
            if (GameInput.KeyPressed(GKey.Escape)) SceneRouter.Go(SceneNames.Title);
        }

        void OnGUI()
        {
            UI.Begin();
            UI.FrameStart();
            float W = UI.W, H = UI.H;

            GUI.Label(new Rect(0f, 60f, W, 30f), "THE KINGDOM OF EMBERFALL", UI.S(24, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ember));
            UI.Shadowed(new Rect(0f, 92f, W, 80f), "Choose Your Battle", UI.S(64, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment), 4f);

            float total = Mathf.Min(1640f, W - 120f);
            float gap = 36f;
            float cw = (total - 2f * gap) / 3f;
            float x0 = (W - total) * 0.5f;
            for (int i = 0; i < Cards.Length; i++)
                DrawCard(Cards[i], new Rect(x0 + i * (cw + gap), 210f, cw, 560f));

            DrawDifficulty(W);

            if (UI.Button(new Rect(x0, H - 104f, 200f, 60f), "Back", BtnKind.Normal, 24)) SceneRouter.Go(SceneNames.Title);
            UI.FrameEnd();
        }

        void DrawCard(LevelCard c, Rect r)
        {
            UI.Panel(r, c.Playable);
            Rect band = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, 150f);
            UI.Rounded(band, new Color(c.Accent.r * 0.35f, c.Accent.g * 0.35f, c.Accent.b * 0.35f, 1f), 10f);
            // Small map vignette: a road winding to a tower
            float bx = band.x + 24f, by = band.y + 40f, bw = band.width - 48f;
            UI.Fill(new Rect(bx, by + 30f, bw * 0.35f, 14f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Fill(new Rect(bx + bw * 0.35f - 14f, by + 30f, 14f, 50f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Fill(new Rect(bx + bw * 0.35f - 14f, by + 66f, bw * 0.45f, 14f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Fill(new Rect(bx + bw * 0.8f - 14f, by + 20f, 14f, 60f), Palette.WithAlpha(Palette.Road, 0.8f));
            UI.Rounded(new Rect(bx + bw * 0.8f - 2f, by - 4f, 50f, 50f), Palette.Stone, 6f);
            UI.Disc(new Rect(bx + bw * 0.8f + 13f, by + 11f, 20f, 20f), Palette.Gold);
            for (int i = 0; i < 5; i++) UI.Disc(new Rect(bx + 8f + i * 22f, by + 30f, 14f, 14f), c.Accent);

            GUI.Label(new Rect(r.x + 24f, r.y + 166f, r.width - 48f, 26f), "LEVEL " + c.Number + "  ·  " + c.Race.ToUpper(), UI.S(19, TextAnchor.MiddleLeft, FontStyle.Bold, c.Accent));
            GUI.Label(new Rect(r.x + 24f, r.y + 196f, r.width - 48f, 48f), c.Name, UI.S(36, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            GUI.Label(new Rect(r.x + 24f, r.y + 252f, r.width - 48f, 90f), c.Setting, UI.S(20, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            GUI.Label(new Rect(r.x + 24f, r.y + 346f, r.width - 48f, 30f), "<color=#A89A86>Boss:</color> " + c.Boss, UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));

            if (c.Playable)
            {
                int stars = GameSession.GetStars(c.Number, GameSession.CurrentDifficulty);
                for (int i = 0; i < 3; i++)
                {
                    Rect s = new Rect(r.x + 24f + i * 46f, r.y + 392f, 36f, 36f);
                    UI.Disc(s, i < stars ? Palette.Gold : new Color(1f, 1f, 1f, 0.08f));
                    UI.Circle(s, i < stars ? Palette.Hex("#FFE9A0") : Palette.PanelBorder, 2f);
                }
                GUI.Label(new Rect(r.x + 170f, r.y + 392f, r.width - 190f, 36f), stars + " / 3 stars on " + GameSession.CurrentDifficulty, UI.S(18, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
                if (UI.Button(new Rect(r.x + 24f, r.yMax - 96f, r.width - 48f, 70f), "Play", BtnKind.Accent, 30))
                {
                    GameSession.SelectedLevel = c.Number;
                    SceneRouter.Go(SceneNames.Level1);
                }
            }
            else
            {
                Rect lockR = new Rect(r.x + 24f, r.yMax - 96f, r.width - 48f, 70f);
                UI.Rounded(lockR, new Color(0f, 0f, 0f, 0.35f), 10f);
                UI.Outline(lockR, Palette.PanelBorder, 2f, 10f);
                GUI.Label(lockR, "LOCKED  <size=18><color=#A89A86>coming in the Beta</color></size>", UI.S(24, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim));
            }
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

using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>Title screen: the Bastion at dusk, embers rising, the Warhost's torches on the horizon.</summary>
    public class TitleScreen : MonoBehaviour
    {
        bool showHowTo;
        float emberTimer;
        readonly List<SpriteRenderer> torches = new List<SpriteRenderer>();
        readonly List<float> torchSeeds = new List<float>();

        void Start()
        {
            Time.timeScale = 1f;
            Art.MakeCamera(Palette.Ink, 5.4f);
            BuildBackdrop();
        }

        void BuildBackdrop()
        {
            Transform root = transform;
            Art.Make("sky", root, Art.Glow, new Color(0.88f, 0.42f, 0.15f, 0.55f), new Vector2(3f, -3.6f), new Vector2(26f, 10f), 0);
            Art.Make("skyHigh", root, Art.Glow, new Color(0.45f, 0.18f, 0.12f, 0.35f), new Vector2(-2f, 1f), new Vector2(26f, 12f), 0);

            // Far mountains
            float[] mx = { -9f, -6.5f, -3.5f, -1f, 2f, 5f, 8.5f };
            float[] mh = { 2.6f, 3.4f, 2.2f, 3.0f, 2.4f, 3.6f, 2.8f };
            for (int i = 0; i < mx.Length; i++)
                Art.Make("mountain", root, Art.Triangle, Palette.Hex("#241A15"), new Vector2(mx[i], -2.6f + mh[i] * 0.5f), new Vector2(mh[i] * 1.6f, mh[i]), 1);

            // Ground and hill
            Art.Make("ground", root, Art.Square, Palette.Hex("#17110E"), new Vector2(0f, -4.5f), new Vector2(24f, 4.2f), 2);
            Art.Make("hill", root, Art.Circle, Palette.Hex("#120D0B"), new Vector2(5.2f, -5.2f), new Vector2(9f, 5f), 3);

            // The Last Bastion
            Vector2 b = new Vector2(5.2f, -1.6f);
            Art.Make("wall", root, Art.Square, Palette.Hex("#2B231E"), b + new Vector2(0f, -0.9f), new Vector2(4.2f, 1.4f), 4);
            Art.Make("keep", root, Art.Square, Palette.Hex("#2B231E"), b + new Vector2(0f, 0.4f), new Vector2(1.6f, 3.2f), 4);
            for (int i = 0; i < 4; i++)
                Art.Make("merlon", root, Art.Square, Palette.Hex("#2B231E"), b + new Vector2(-0.6f + i * 0.4f, 2.1f), new Vector2(0.24f, 0.3f), 4);
            for (int i = 0; i < 6; i++)
                Art.Make("wallMerlon", root, Art.Square, Palette.Hex("#2B231E"), b + new Vector2(-2f + i * 0.8f, -0.1f), new Vector2(0.3f, 0.3f), 4);
            Art.Make("window", root, Art.Square, Palette.Ember, b + new Vector2(0f, 0.9f), new Vector2(0.22f, 0.4f), 5);
            Art.Make("window2", root, Art.Square, Palette.Ember, b + new Vector2(0f, 0f), new Vector2(0.22f, 0.4f), 5);
            Art.Make("gate", root, Art.Square, Palette.Hex("#C25A1A"), b + new Vector2(0f, -1.25f), new Vector2(0.6f, 0.7f), 5);
            Art.Make("brazierGlow", root, Art.Glow, new Color(1f, 0.6f, 0.2f, 0.9f), b + new Vector2(0f, 2.6f), new Vector2(2.4f, 2.4f), 5);
            Art.Make("brazier", root, Art.Circle, Palette.Gold, b + new Vector2(0f, 2.4f), new Vector2(0.28f, 0.28f), 6);
            Art.Make("flagpole", root, Art.Square, Palette.Hex("#2B231E"), b + new Vector2(0.65f, 2.6f), new Vector2(0.05f, 1f), 5);
            Art.Make("flag", root, Art.Triangle, Palette.Ember, b + new Vector2(0.95f, 2.9f), new Vector2(0.4f, 0.55f), 5, -90f);

            // The Warhost's torches on the horizon (orange) and the restless dead (green)
            for (int i = 0; i < 40; i++)
            {
                bool undead = i % 4 == 0;
                Vector2 p = new Vector2(Random.Range(-9.5f, 0.5f), Random.Range(-3.0f, -2.4f));
                var t = Art.Make("torch", root, Art.Glow, undead ? new Color(0.55f, 0.9f, 0.4f, 0.8f) : new Color(1f, 0.6f, 0.2f, 0.8f), p, Vector2.one * Random.Range(0.12f, 0.24f), 3);
                torches.Add(t);
                torchSeeds.Add(Random.value * 10f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            emberTimer -= dt;
            if (emberTimer <= 0f)
            {
                emberTimer = 0.07f;
                Vector3 p = new Vector3(Random.Range(-10f, 10f), -5.6f, 0f);
                Fx.Burst(p, new Color(1f, Random.Range(0.4f, 0.7f), 0.15f, 0.9f), Random.Range(0.06f, 0.14f), 0.02f, Random.Range(4f, 7f), Art.Circle, 7, new Vector3(Random.Range(-0.25f, 0.35f), Random.Range(0.8f, 1.6f), 0f));
            }

            for (int i = 0; i < torches.Count; i++)
            {
                var t = torches[i];
                Color c = t.color;
                c.a = 0.45f + 0.4f * Mathf.PerlinNoise(Time.time * 3f, torchSeeds[i]);
                t.color = c;
                Vector3 pos = t.transform.position;
                pos.x += dt * 0.08f;
                if (pos.x > 1f) pos.x = -9.5f;
                t.transform.position = pos;
            }

            if (showHowTo && GameInput.KeyPressed(GKey.Escape)) showHowTo = false;
        }

        void OnGUI()
        {
            UI.Begin();
            UI.FrameStart();
            float W = UI.W, H = UI.H;
            float left = Mathf.Max(80f, W * 0.08f);

            GUI.Label(new Rect(left, 150f, 900f, 30f), "PHOENIX STUDIOS PRESENTS", UI.S(24, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Ember));
            UI.Shadowed(new Rect(left - 6f, 180f, 1100f, 160f), "LAST BASTION", UI.S(132, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment), 5f);
            GUI.Label(new Rect(left, 340f, 900f, 40f), "Hold the line. Build every tower. Save Emberfall.", UI.S(30, TextAnchor.MiddleLeft, FontStyle.Italic, Palette.Muted));

            bool prev = GUI.enabled;
            GUI.enabled = !showHowTo;
            float y = 480f;
            if (UI.Button(new Rect(left, y, 440f, 76f), "Campaign", BtnKind.Accent, 30)) SceneRouter.Go(SceneNames.LevelSelect);
            y += 92f;
            if (UI.Button(new Rect(left, y, 440f, 68f), "How to Play", BtnKind.Normal, 26)) showHowTo = true;
            y += 84f;
            if (UI.Button(new Rect(left, y, 440f, 68f), "Quit", BtnKind.Normal, 26)) SceneRouter.Quit();
            GUI.enabled = prev;

            GUI.Label(new Rect(left, H - 64f, 900f, 30f), "Alpha build  ·  Brian Hunt, Aiden Tjahjono, Micheal Biaggini", UI.S(20, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));

            if (showHowTo) DrawHowTo(W, H);
            UI.FrameEnd();
        }

        void DrawHowTo(float W, float H)
        {
            UI.Veil(0.7f);
            float w = Mathf.Min(1100f, W - 120f), h = 760f;
            Rect r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            UI.Panel(r, true);
            GUI.Label(new Rect(r.x + 40f, r.y + 28f, w - 80f, 50f), "How to Play", UI.S(44, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));

            string goal =
                "<b>Goal:</b> Orcs march down the road toward the Last Bastion. Every orc that reaches the gate costs lives. " +
                "Survive all 10 waves and defeat <b>Gorruk Skullsplitter</b> to win.\n\n" +
                "<b>Build:</b> click a glowing stone plot, then pick an Archer Tower, Catapult or Mage Tower. Defeated orcs drop gold.\n" +
                "<b>Upgrade:</b> click a tower. Each has Power, Speed and Utility branches. Take one to Tier 3 and a second to Tier 2.\n" +
                "<b>Hero:</b> Sir Aldric blocks up to 3 orcs and fights them. Right-click to move him. Press <b>Q</b> for Shield Slam.\n" +
                "<b>Watch out:</b> War Drummers slow nearby towers, and the Ember Vent burns orcs and Sir Aldric alike.";
            GUI.Label(new Rect(r.x + 40f, r.y + 96f, w - 80f, 300f), goal, UI.S(22, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));

            string[,] keys =
            {
                { "Left click", "Build on a plot / select a tower or hero" },
                { "Right click", "Move Sir Aldric" },
                { "Q", "Shield Slam (damage and stun nearby orcs)" },
                { "Space", "Start the next wave early for bonus gold" },
                { "WASD / Arrows, Wheel", "Pan and zoom the camera (middle-drag pans too)" },
                { "F  /  Esc  /  H", "Toggle 2x speed  /  pause  /  find the hero" },
            };
            float y = r.y + 410f;
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                Rect k = new Rect(r.x + 40f, y, 300f, 40f);
                UI.Rounded(k, new Color(1f, 1f, 1f, 0.06f), 8f);
                GUI.Label(k, keys[i, 0], UI.S(21, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Gold));
                GUI.Label(new Rect(r.x + 360f, y, w - 400f, 40f), keys[i, 1], UI.S(21, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Parchment));
                y += 46f;
            }

            if (UI.Button(new Rect(r.xMax - 220f, r.y + 28f, 180f, 52f), "Close", BtnKind.Normal, 22)) showHowTo = false;
        }
    }
}

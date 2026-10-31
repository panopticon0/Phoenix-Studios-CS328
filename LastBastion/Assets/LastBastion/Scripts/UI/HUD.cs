using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// In-level HUD. The HUD mechanic is the command bar the whole game is played through:
    /// gold / lives / wave tracker, a wave scout preview with "Call Early" for bonus gold,
    /// Sir Aldric's card with Shield Slam cooldown, the boss health bar, the build menu,
    /// the tower upgrade-tree panel, Captain Brand's alerts, and world popups.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        LevelManager lm;

        void Awake()
        {
            lm = GetComponent<LevelManager>();
        }

        void OnGUI()
        {
            if (lm == null || lm.Cam == null || lm.HeroUnit == null) return;
            UI.Begin();
            UI.FrameStart();
            float W = UI.W, H = UI.H;
            bool playing = lm.State == GameState.Playing;

            DrawPopups();
            DrawVentWarning();

            bool prev = GUI.enabled;
            GUI.enabled = playing;
            DrawTopBar(W);
            DrawWavePanel(W);
            DrawBossBar(W);
            DrawHeroCard(H);
            DrawControlsHint(W, H);
            if (Event.current.type == EventType.Repaint) lm.BuildPreviewRange = 0f;
            if (lm.SelectedPlot != null) DrawBuildMenu(W, H);
            if (lm.SelectedTower != null) DrawTowerPanel(W, H);
            GUI.enabled = prev;

            DrawAnnouncement(W);

            if (lm.State == GameState.Paused) DrawPause(W, H);
            else if (lm.State == GameState.Victory || lm.State == GameState.Defeat) DrawEnd(W, H);

            UI.FrameEnd();
        }

        // ------------------------------------------------------------ World overlays

        void DrawPopups()
        {
            foreach (var p in lm.Popups)
            {
                Vector2 g = UI.WorldToGUI(lm.Cam, p.World);
                float a = 1f - Mathf.Clamp01((p.Age - 0.8f) / 0.6f);
                Color c = p.Color;
                c.a = a;
                Color shadow = new Color(0f, 0f, 0f, 0.7f * a);
                Rect r = new Rect(g.x - 200f, g.y - 16f, 400f, 32f);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), p.Text, UI.S(24, TextAnchor.MiddleCenter, FontStyle.Bold, shadow));
                GUI.Label(r, p.Text, UI.S(24, TextAnchor.MiddleCenter, FontStyle.Bold, c));
            }
        }

        void DrawVentWarning()
        {
            var v = lm.Vent;
            if (v == null || v.Current == EmberVent.Phase.Dormant) return;
            Vector2 g = UI.WorldToGUI(lm.Cam, v.Position + Vector2.up * (EmberVent.Radius + 0.6f));
            string text = v.Current == EmberVent.Phase.Warning ? "ERUPTING IN " + Mathf.CeilToInt(v.TimeLeft) : "ERUPTING!";
            Color c = v.Current == EmberVent.Phase.Warning ? Palette.Gold : Palette.Ember;
            Rect r = new Rect(g.x - 120f, g.y - 18f, 240f, 36f);
            UI.Rounded(r, new Color(0f, 0f, 0f, 0.65f), 18f);
            GUI.Label(r, text, UI.S(22, TextAnchor.MiddleCenter, FontStyle.Bold, c));
        }

        // ------------------------------------------------------------ Top bar

        void DrawTopBar(float W)
        {
            Rect bar = UI.Block(new Rect(0f, 0f, W, 64f));
            UI.Fill(bar, new Color(0.07f, 0.055f, 0.045f, 0.94f));
            UI.Fill(new Rect(0f, 62f, W, 2f), Palette.PanelBorder);

            float x = 24f;
            UI.Disc(new Rect(x, 17f, 30f, 30f), Palette.Gold);
            UI.Circle(new Rect(x + 6f, 23f, 18f, 18f), Palette.EmberDeep, 2f);
            GUI.Label(new Rect(x + 42f, 0f, 150f, 64f), lm.Gold.ToString(), UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Gold));
            x += 190f;

            UI.Disc(new Rect(x, 17f, 30f, 30f), Palette.Bad);
            GUI.Label(new Rect(x + 42f, 0f, 160f, 64f), lm.Lives + " / " + lm.MaxLives, UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            x += 200f;

            GUI.Label(new Rect(x, 0f, 260f, 64f), "Wave " + lm.WaveNumber + " / " + lm.WaveCount, UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));

            GUI.Label(new Rect(W * 0.5f - 300f, 0f, 600f, 64f), Level1Data.Name.ToUpper() + "   <color=#A89A86><size=20>" + GameSession.CurrentDifficulty + "</size></color>", UI.S(30, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ember));

            if (UI.Button(new Rect(W - 300f, 10f, 140f, 44f), lm.FastForward ? "Speed 2x [F]" : "Speed 1x [F]", lm.FastForward ? BtnKind.Accent : BtnKind.Normal, 20))
                lm.ToggleSpeed();
            if (UI.Button(new Rect(W - 148f, 10f, 124f, 44f), "Pause [Esc]", BtnKind.Normal, 20))
                lm.Pause();
        }

        void DrawWavePanel(float W)
        {
            if (lm.WaveNumber >= lm.WaveCount && !lm.WaveCountdown)
            {
                Rect f = UI.Block(new Rect(W - 384f, 76f, 360f, 64f));
                UI.Panel(f);
                GUI.Label(new Rect(f.x + 16f, f.y, f.width - 32f, f.height), "Final wave! Enemies left: " + lm.Enemies.Count, UI.S(22, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Bad));
                return;
            }

            if (!lm.WaveCountdown)
            {
                Rect s = UI.Block(new Rect(W - 384f, 76f, 360f, 64f));
                UI.Panel(s);
                GUI.Label(new Rect(s.x + 16f, s.y, s.width - 32f, s.height), "Wave " + lm.WaveNumber + " attacking · " + lm.Enemies.Count + " orcs on the road", UI.S(21, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Muted));
                return;
            }

            Rect r = UI.Block(new Rect(W - 384f, 76f, 360f, 176f));
            UI.Panel(r, true);
            string head = lm.WaveNumber == 0 ? "The Warhost approaches" : "Wave " + (lm.WaveNumber + 1) + " in " + Mathf.CeilToInt(lm.NextWaveTimer) + "s";
            GUI.Label(new Rect(r.x + 16f, r.y + 10f, r.width - 32f, 34f), head, UI.S(26, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            GUI.Label(new Rect(r.x + 16f, r.y + 46f, r.width - 32f, 50f), "<color=#A89A86>Scout:</color> " + lm.NextWavePreview, UI.S(19, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            string btn = lm.WaveNumber == 0 ? "Start Wave 1  [Space]" : lm.EarlyBonus > 0 ? "Call Early  +" + lm.EarlyBonus + "g  [Space]" : "Call Early  [Space]";
            if (UI.Button(new Rect(r.x + 16f, r.y + 104f, r.width - 32f, 56f), btn, BtnKind.Accent, 22))
                lm.StartNextWave(true);
        }

        void DrawBossBar(float W)
        {
            var b = lm.Boss;
            if (b == null || !b.Alive) return;
            float w = Mathf.Clamp(W - 2f * 420f, 420f, 900f);
            Rect r = UI.Block(new Rect((W - w) * 0.5f, 76f, w, 84f));
            UI.Panel(r, b.Enraged);
            string title = "GORRUK SKULLSPLITTER  <color=#A89A86>Orc Warlord</color>";
            if (b.Enraged) title += "   <color=#D9533B>ENRAGED</color>";
            if (b.Telegraphing) title += "   <color=#E8C547>AXE INCOMING!</color>";
            GUI.Label(new Rect(r.x + 16f, r.y + 6f, w - 32f, 34f), title, UI.S(24, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment));
            Rect hp = new Rect(r.x + 20f, r.y + 46f, w - 40f, 22f);
            UI.Bar(hp, b.Hp / b.MaxHp, Palette.Bad, new Color(0f, 0f, 0f, 0.6f));
            UI.Fill(new Rect(hp.x + hp.width * 0.5f - 1f, hp.y - 4f, 2f, hp.height + 8f), Palette.Gold);
        }

        // ------------------------------------------------------------ Hero card

        void DrawHeroCard(float H)
        {
            var h = lm.HeroUnit;
            Rect r = UI.Block(new Rect(24f, H - 196f, 480f, 172f));
            UI.Panel(r, lm.HeroSelected);

            Rect portrait = new Rect(r.x + 16f, r.y + 16f, 104f, 104f);
            UI.Disc(portrait, h.Dead ? Palette.StoneDark : Palette.HeroBlue);
            UI.Circle(portrait, Palette.Gold, 4f);
            UI.Disc(new Rect(portrait.x + 30f, portrait.y + 22f, 44f, 44f), h.Dead ? Palette.Dim : Palette.Hex("#C8C2BA"));
            UI.Rounded(new Rect(portrait.x + 24f, portrait.y + 70f, 56f, 18f), Palette.Hex("#8E2A1E"), 6f);
            if (GUI.Button(portrait, GUIContent.none, GUIStyle.none) && !h.Dead)
            {
                lm.SelectHero();
                lm.CamCtl.Focus(h.Position);
            }
            GUI.Label(new Rect(portrait.x, portrait.yMax + 4f, portrait.width, 24f), "[H] find", UI.S(18, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Dim));

            float x = r.x + 136f, w = r.width - 152f;
            GUI.Label(new Rect(x, r.y + 12f, w, 30f), Hero.HeroName, UI.S(24, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            if (h.Dead)
            {
                GUI.Label(new Rect(x, r.y + 46f, w, 30f), "Respawning at the gate in " + Mathf.CeilToInt(h.RespawnLeft) + "s", UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Bad));
            }
            else
            {
                UI.Bar(new Rect(x, r.y + 52f, w, 18f), h.Hp / Hero.MaxHp, h.Hp / Hero.MaxHp > 0.35f ? Palette.Good : Palette.Bad, new Color(0f, 0f, 0f, 0.6f));
                string status = h.Moving ? "Moving" : h.BlockingCount > 0 ? "Blocking " + h.BlockingCount + " / 3" : "Holding position";
                GUI.Label(new Rect(x, r.y + 72f, w, 24f), Mathf.CeilToInt(h.Hp) + " / " + Mathf.CeilToInt(Hero.MaxHp) + "  ·  " + status, UI.S(18, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));
            }

            bool prev = GUI.enabled;
            GUI.enabled = prev && h.AbilityReady;
            Rect ab = new Rect(x, r.y + 108f, w, 48f);
            string label = h.AbilityReady ? "Shield Slam  [Q]" : h.Dead ? "Shield Slam" : "Shield Slam  " + Mathf.CeilToInt(h.AbilityLeft) + "s";
            if (UI.Button(ab, label, BtnKind.Normal, 22)) h.UseAbility();
            GUI.enabled = prev;
            if (!h.AbilityReady && !h.Dead)
                UI.Fill(new Rect(ab.x + 4f, ab.yMax - 8f, (ab.width - 8f) * (1f - h.AbilityLeft / Hero.AbilityCooldown), 4f), Palette.Ember);
        }

        void DrawControlsHint(float W, float H)
        {
            string text;
            if (lm.HeroSelected) text = "<b>Sir Aldric selected:</b> click the ground or right-click to move him.";
            else if (lm.SelectedPlot != null) text = "Pick a tower, or press <b>1 / 2 / 3</b>.  Esc to cancel.";
            else text = "<b>LMB</b> ground by the road: build  /  select   <b>RMB</b> move hero   <b>Q</b> Shield Slam   <b>WASD</b> pan   <b>Wheel</b> zoom   <b>Space</b> next wave";
            Rect r = new Rect(520f, H - 52f, W - 540f, 36f);
            if (r.width < 300f) return;
            GUI.Label(r, text, UI.S(19, TextAnchor.MiddleRight, FontStyle.Normal, Palette.Dim));
        }

        // ------------------------------------------------------------ Build menu

        void DrawBuildMenu(float W, float H)
        {
            var plot = lm.SelectedPlot;
            Vector2 sp = UI.WorldToGUI(lm.Cam, plot.Position);
            float w = 560f, h = 268f;
            Rect r = new Rect(sp.x - w * 0.5f, sp.y + 56f, w, h);
            if (r.yMax > H - 16f) r.y = sp.y - 56f - h;
            r.x = Mathf.Clamp(r.x, 16f, W - w - 16f);
            r.y = Mathf.Clamp(r.y, 76f, H - h - 16f);
            UI.Block(r);
            UI.Panel(r, true);

            GUI.Label(new Rect(r.x + 18f, r.y + 10f, w - 80f, 36f), "Build a tower", UI.S(26, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            if (UI.Button(new Rect(r.xMax - 52f, r.y + 10f, 36f, 36f), "X", BtnKind.Ghost, 20)) lm.ClearSelection();

            for (int i = 0; i < TowerData.BuildOrder.Length; i++)
            {
                var type = TowerData.BuildOrder[i];
                var def = TowerData.Get(type);
                bool afford = lm.Gold >= def.Cost;
                Rect b = new Rect(r.x + 16f + i * 178f, r.y + 58f, 170f, 194f);
                bool prev = GUI.enabled;
                GUI.enabled = prev && afford;
                if (UI.Button(b, "", BtnKind.Normal)) lm.TryBuild(plot, type);
                if (Event.current.type == EventType.Repaint && b.Contains(Event.current.mousePosition)) lm.BuildPreviewRange = def.Range;
                GUI.enabled = prev;

                Rect icon = new Rect(b.x + b.width * 0.5f - 30f, b.y + 14f, 60f, 60f);
                UI.Rounded(icon, Palette.Stone, 8f);
                UI.Disc(new Rect(icon.x + 12f, icon.y + 12f, 36f, 36f), def.Roof);
                GUI.Label(new Rect(b.x + 6f, b.y + 80f, b.width - 12f, 28f), def.Name, UI.S(21, TextAnchor.MiddleCenter, FontStyle.Bold, afford ? Palette.Parchment : Palette.Dim));
                GUI.Label(new Rect(b.x + 6f, b.y + 108f, b.width - 12f, 28f), def.Cost + " gold", UI.S(21, TextAnchor.MiddleCenter, FontStyle.Bold, afford ? Palette.Gold : Palette.Bad));
                GUI.Label(new Rect(b.x + 8f, b.y + 136f, b.width - 16f, 30f), def.Role, UI.S(17, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Muted, true));
                GUI.Label(new Rect(b.x + 6f, b.y + 164f, b.width - 12f, 24f), "[" + (i + 1) + "]", UI.S(17, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Dim));
            }
        }

        // ------------------------------------------------------------ Tower panel

        void DrawTowerPanel(float W, float H)
        {
            var tw = lm.SelectedTower;
            if (tw == null) return;
            float w = 660f, h = 600f;
            Rect r = new Rect(W - w - 24f, 264f, w, h);
            if (r.yMax > H - 24f) r.y = Mathf.Max(76f, H - 24f - h);
            UI.Block(r);
            UI.Panel(r, true);

            GUI.Label(new Rect(r.x + 20f, r.y + 12f, w - 90f, 36f), tw.Def.Name, UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            if (UI.Button(new Rect(r.xMax - 54f, r.y + 12f, 36f, 36f), "X", BtnKind.Ghost, 20)) { lm.ClearSelection(); return; }

            string kind = tw.Kind == DamageKind.Magic ? "magic" : tw.Kind == DamageKind.True ? "armor-piercing" : "physical";
            string stats = "Damage <b>" + tw.Damage.ToString("0.#") + "</b> " + kind + "   ·   " + (1f / tw.Interval).ToString("0.00") + " attacks/s   ·   Range <b>" + tw.Range.ToString("0.0") + "</b>";
            GUI.Label(new Rect(r.x + 20f, r.y + 54f, w - 40f, 28f), stats, UI.S(20, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));
            var specials = tw.SpecialLines();
            string special = specials.Count > 0 ? string.Join("  ·  ", specials.ToArray()) : "No special abilities yet. Upgrade a branch below.";
            GUI.Label(new Rect(r.x + 20f, r.y + 84f, w - 40f, 44f), special, UI.S(18, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Dim, true));

            string statusText = null;
            Color statusColor = Palette.Bad;
            if (tw.Disabled) statusText = "KNOCKED OUT by Gorruk's axe: " + tw.DisabledLeft.ToString("0.0") + "s";
            else if (tw.SlowedByDrummer) { statusText = "Slowed 30% by a War Drummer's aura"; statusColor = Palette.Ember; }
            else if (tw.Overcharged) { statusText = "OVERCHARGED: attacking twice as fast"; statusColor = Palette.Magic; }
            if (statusText != null)
            {
                Rect sr = new Rect(r.x + 20f, r.y + 130f, w - 40f, 32f);
                UI.Rounded(sr, new Color(statusColor.r, statusColor.g, statusColor.b, 0.18f), 8f);
                GUI.Label(sr, statusText, UI.S(19, TextAnchor.MiddleCenter, FontStyle.Bold, statusColor));
            }

            // Upgrade tree: three branch columns
            float colW = (w - 40f - 2f * 12f) / 3f;
            float top = r.y + 174f;
            for (int b = 0; b < 3; b++)
            {
                Rect col = new Rect(r.x + 20f + b * (colW + 12f), top, colW, 340f);
                UI.Rounded(col, new Color(1f, 1f, 1f, 0.04f), 10f);
                UI.Fill(new Rect(col.x, col.y, col.width, 4f), Palette.Branch(b));
                GUI.Label(new Rect(col.x + 12f, col.y + 10f, col.width - 24f, 28f), TowerData.BranchNames[b].ToUpper(), UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Branch(b)));

                int level = tw.Levels[b];
                for (int tier = 0; tier < 3; tier++)
                {
                    var up = tw.Def.Upgrades[b, tier];
                    bool owned = level > tier;
                    bool next = level == tier;
                    Rect row = new Rect(col.x + 12f, col.y + 44f + tier * 30f, col.width - 24f, 28f);
                    UI.Disc(new Rect(row.x, row.y + 7f, 14f, 14f), owned ? Palette.Branch(b) : new Color(1f, 1f, 1f, 0.12f));
                    Color tc = owned ? Palette.Parchment : next ? Palette.Muted : Palette.Hex("#6E6258");
                    GUI.Label(new Rect(row.x + 22f, row.y, row.width - 22f, row.height), "T" + (tier + 1) + "  " + up.Name, UI.S(18, TextAnchor.MiddleLeft, owned ? FontStyle.Bold : FontStyle.Normal, tc));
                }

                Rect descR = new Rect(col.x + 12f, col.y + 142f, col.width - 24f, 110f);
                Rect btnR = new Rect(col.x + 12f, col.yMax - 66f, col.width - 24f, 54f);
                if (level >= 3)
                {
                    GUI.Label(descR, "Branch complete.", UI.S(18, TextAnchor.UpperLeft, FontStyle.Italic, Palette.Dim, true));
                    GUI.Label(btnR, "MAXED", UI.S(22, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Branch(b)));
                    continue;
                }

                var nextUp = tw.Def.Upgrades[b, level];
                GUI.Label(descR, "<b>" + nextUp.Name + "</b>\n" + nextUp.Desc, UI.S(18, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
                string reason;
                bool allowed = tw.CanUpgrade(b, out reason);
                int cost = tw.NextCost(b);
                if (!allowed)
                {
                    UI.Rounded(btnR, new Color(0f, 0f, 0f, 0.3f), 8f);
                    GUI.Label(btnR, "Locked\n<size=15>2 branches max</size>", UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim));
                    continue;
                }
                bool prev = GUI.enabled;
                GUI.enabled = prev && lm.Gold >= cost;
                if (UI.Button(btnR, "Upgrade  " + cost + "g", BtnKind.Accent, 21)) lm.TryUpgrade(tw, b);
                GUI.enabled = prev;
            }

            GUI.Label(new Rect(r.x + 20f, r.yMax - 76f, w - 220f, 60f), "One branch can reach Tier 3 and a second Tier 2. The third branch stays locked.", UI.S(16, TextAnchor.MiddleLeft, FontStyle.Italic, Palette.Dim, true));
            if (UI.Button(new Rect(r.xMax - 196f, r.yMax - 72f, 176f, 52f), "Sell  +" + tw.SellValue + "g", BtnKind.Danger, 21)) lm.Sell(tw);
        }

        // ------------------------------------------------------------ Announcements

        void DrawAnnouncement(float W)
        {
            if (lm.Announcements.Count == 0) return;
            var a = lm.Announcements[0];
            float fadeIn = Mathf.Clamp01(a.Age / 0.25f);
            float fadeOut = Mathf.Clamp01((a.Duration - a.Age) / 0.5f);
            float alpha = Mathf.Min(fadeIn, fadeOut);
            float w = Mathf.Min(860f, W - 80f);
            float y = lm.Boss != null ? 172f : 84f;
            Rect r = new Rect((W - w) * 0.5f, y, w, 132f);

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UI.Rounded(r, new Color(0.08f, 0.06f, 0.05f, 0.94f), 12f);
            UI.Outline(r, a.Accent, 3f, 12f);
            UI.Fill(new Rect(r.x, r.y + 12f, 6f, r.height - 24f), a.Accent);
            GUI.Label(new Rect(r.x + 26f, r.y + 10f, w - 52f, 34f), a.Title, UI.S(26, TextAnchor.MiddleLeft, FontStyle.Bold, a.Accent));
            string speaker = string.IsNullOrEmpty(a.Speaker) ? "" : "<color=#E8C547>" + a.Speaker + ":</color> ";
            GUI.Label(new Rect(r.x + 26f, r.y + 48f, w - 52f, 78f), speaker + a.Body, UI.S(19, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            GUI.color = prevColor;
        }

        // ------------------------------------------------------------ Overlays

        void DrawPause(float W, float H)
        {
            UI.Veil(0.6f);
            float w = 520f, h = 520f;
            Rect r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            UI.Panel(r, true);
            UI.Shadowed(new Rect(r.x, r.y + 24f, w, 60f), "Paused", UI.S(52, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment), 3f);
            float bx = r.x + 60f, bw = w - 120f, y = r.y + 116f;
            if (UI.Button(new Rect(bx, y, bw, 64f), "Resume", BtnKind.Accent)) lm.Resume();
            y += 80f;
            if (UI.Button(new Rect(bx, y, bw, 64f), "Restart Level")) lm.Restart();
            y += 80f;
            if (UI.Button(new Rect(bx, y, bw, 64f), "Level Select")) lm.ToLevelSelect();
            y += 80f;
            if (UI.Button(new Rect(bx, y, bw, 64f), "Title Screen")) lm.ToTitle();
            GUI.Label(new Rect(r.x + 20f, r.yMax - 60f, w - 40f, 40f), "Debug: F9 = +500 gold   ·   F10 = skip to boss", UI.S(17, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Dim));
        }

        void DrawEnd(float W, float H)
        {
            bool won = lm.State == GameState.Victory;
            UI.Veil(0.65f);
            float w = 680f, h = 560f;
            Rect r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            UI.Panel(r, true);
            string title = won ? "VICTORY" : "THE BASTION HAS FALLEN";
            UI.Shadowed(new Rect(r.x, r.y + 26f, w, 70f), title, UI.S(won ? 64 : 44, TextAnchor.MiddleCenter, FontStyle.Bold, won ? Palette.Gold : Palette.Bad), 3f);
            string sub = won ? "The Ashen Pass holds. Gorruk's Warhost is broken." : "The orcs broke through the gate. Rebuild and try again, Commander.";
            GUI.Label(new Rect(r.x + 40f, r.y + 104f, w - 80f, 60f), sub, UI.S(22, TextAnchor.MiddleCenter, FontStyle.Italic, Palette.Muted, true));

            if (won)
            {
                float sx = r.x + w * 0.5f - 3f * 44f;
                for (int i = 0; i < 3; i++)
                {
                    Rect s = new Rect(sx + i * 88f, r.y + 176f, 76f, 76f);
                    bool lit = i < lm.StarsEarned;
                    UI.Disc(s, lit ? Palette.Gold : new Color(1f, 1f, 1f, 0.08f));
                    UI.Circle(s, lit ? Palette.Hex("#FFE9A0") : Palette.PanelBorder, 3f);
                }
            }

            int mins = Mathf.FloorToInt(lm.ElapsedTime / 60f), secs = Mathf.FloorToInt(lm.ElapsedTime % 60f);
            string stats = "Lives left: <b>" + lm.Lives + " / " + lm.MaxLives + "</b>     Waves: <b>" + lm.WaveNumber + " / " + lm.WaveCount + "</b>\nOrcs defeated: <b>" + lm.Kills + "</b>     Time: <b>" + mins + ":" + secs.ToString("00") + "</b>";
            GUI.Label(new Rect(r.x + 40f, r.y + 272f, w - 80f, 70f), stats, UI.S(22, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Parchment, true));

            float bw = (w - 80f - 24f) / 3f, y = r.yMax - 110f;
            if (UI.Button(new Rect(r.x + 40f, y, bw, 64f), won ? "Play Again" : "Try Again", BtnKind.Accent, 22)) lm.Restart();
            if (UI.Button(new Rect(r.x + 40f + bw + 12f, y, bw, 64f), "Level Select", BtnKind.Normal, 22)) lm.ToLevelSelect();
            if (UI.Button(new Rect(r.x + 40f + 2f * (bw + 12f), y, bw, 64f), "Title Screen", BtnKind.Normal, 22)) lm.ToTitle();
        }
    }
}

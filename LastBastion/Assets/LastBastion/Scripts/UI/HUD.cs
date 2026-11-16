using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// In-level HUD (beta). The HUD mechanic is a Bloons-style command layout:
    ///  - a shop sidebar on the right: drag a tower (or Dragonfire) onto the map, or click it then click the map;
    ///  - a contextual upgrade panel that docks on the side of the screen away from the selected tower,
    ///    with three upgrade paths, targeting priority and sell;
    ///  - The hero's card with a radial Shield Slam clock, level/XP and hero points, and his skill-tree panel;
    ///  - the wave box (scout preview + Start / Call Early), boss bar with wind-up warnings,
    ///    compact advisor toasts at the bottom, tutorial objectives, world labels and popups.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        const float ShopW = 248f;
        const float PanelW = 372f;

        LevelManager lm;
        Rect archerCard, waveButton, slamButton, powerCard, heroPortrait;
        string tipTitle, tipBody;
        Rect tipAnchor;
        bool tipLeft;

        void Awake()
        {
            lm = GetComponent<LevelManager>();
        }

        void OnGUI()
        {
            if (lm == null || lm.Cam == null || lm.HeroUnit == null || lm.Level == null) return;
            UI.Begin();
            UI.FrameStart();
            float W = UI.W, H = UI.H;
            bool playing = lm.State == GameState.Playing;
            tipTitle = null;

            DrawWorldLabels(W, H);
            DrawPopups();

            bool prev = GUI.enabled;
            GUI.enabled = playing;
            DrawTopBar(W);

            // Bloons-style: when the upgrade/hero panel docks on the right it REPLACES the shop.
            // (Drawing the shop underneath let its drag-cards grab clicks meant for the BUY buttons.)
            bool panelOverShop = false;
            if (lm.SelectedTower != null || lm.HeroSelected)
            {
                Vector2 wp = lm.SelectedTower != null ? lm.SelectedTower.Position : (lm.HeroUnit.Dead ? lm.HeroUnit.RespawnPoint : lm.HeroUnit.Position);
                panelOverShop = DockRect(wp, W, H).x > W * 0.5f;
            }
            if (!panelOverShop) DrawShop(W, H);
            else { archerCard = new Rect(0, 0, 0, 0); powerCard = new Rect(0, 0, 0, 0); }
            DrawWaveBox(W, H);
            DrawHeroCard(H);
            DrawBossBar(W);
            if (lm.SelectedTower != null) DrawTowerPanel(W, H);
            else if (lm.HeroSelected) DrawHeroPanel(W, H);
            // Bottom row beside the hero card: tutorial objective, level-up panel, advisor toast, key hints.
            float rowX = 532f, rowRight = W - ShopW - 24f;
            rowX = DrawObjectives(rowX, rowRight, H);
            rowX = DrawLevelUpPanel(rowX, rowRight, H);
            rowX = DrawToasts(rowX, rowRight, W, H);
            DrawHint(rowX, rowRight, H);
            DrawTutorialPointer();
            if (tipTitle != null) UI.Tooltip(tipAnchor, tipTitle, tipBody, tipLeft);
            GUI.enabled = prev;

            if (lm.State == GameState.Paused) DrawPause(W, H);
            else if (lm.State == GameState.Victory || lm.State == GameState.Defeat) DrawEnd(W, H);

            UI.FrameEnd();
        }

        // ================================================================== World overlays

        Vector2 G(Vector3 world) => UI.WorldToGUI(lm.Cam, world);

        void WorldLabel(Vector3 world, string text, Color color, int size = 20, float yOff = 0f)
        {
            Vector2 g = G(world);
            float w = Mathf.Max(80f, text.Length * size * 0.55f + 24f);
            Rect r = new Rect(g.x - w * 0.5f, g.y - 16f + yOff, w, size + 12f);
            UI.Rounded(r, new Color(0f, 0f, 0f, 0.6f), 10f);
            GUI.Label(r, text, UI.S(size, TextAnchor.MiddleCenter, FontStyle.Bold, color));
        }

        void DrawWorldLabels(float W, float H)
        {
            // Ember Vent countdown
            foreach (var v in lm.Vents)
            {
                if (v == null || v.Current == EmberVent.Phase.Dormant) continue;
                bool warn = v.Current == EmberVent.Phase.Warning;
                WorldLabel(v.Position + Vector2.up * (EmberVent.Radius + 0.6f), warn ? "ERUPTING IN " + Mathf.CeilToInt(v.TimeLeft) : "ERUPTING!", warn ? Palette.Gold : Palette.Ember, 20);
            }

            // Rockslide cliff
            if (lm.Cliff != null)
            {
                var c = lm.Cliff;
                if (c.Ready) WorldLabel(c.Position + Vector2.down * 1.9f, c.DisplayName.ToUpper() + " READY: click or R", Palette.HeroGlow, 18);
                else if (c.Current == Rockslide.Phase.Recharging) WorldLabel(c.Position + Vector2.down * 1.9f, c.DisplayName + " recharging " + Mathf.CeilToInt(c.RechargeLeft) + "s", Palette.Dim, 16);
            }

            // Ash Storm
            if (lm.Storm != null && lm.Storm.Active)
                WorldLabel(lm.Storm.Center + Vector2.up * (AshStorm.Radius * 0.6f), lm.Storm.DisplayName.ToUpper() + ": towers inside -40% range", Palette.Muted, 18);

            // Boss wind-up: STUN NOW cue
            if (lm.Boss != null && lm.Boss.Alive && lm.Boss.WindingUp)
            {
                bool flash = Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f;
                WorldLabel(lm.Boss.Position + Vector2.up * (lm.Boss.Def.Radius + 1.1f), lm.Boss.WindUpName + ": STUN TO INTERRUPT!", flash ? Palette.Interrupt : Palette.Parchment, 20);
            }

            // Hero nameplate, respawn countdown, and off-screen arrow
            var h = lm.HeroUnit;
            if (h.Dead)
            {
                WorldLabel(h.RespawnPoint + Vector2.up * 1.3f, h.Name + " returns in " + Mathf.CeilToInt(h.RespawnLeft) + "s", Palette.HeroGlow, 18);
            }
            else
            {
                string plate = h.Name.ToUpper() + "  Lv " + h.Level + (h.Points > 0 ? "  <color=#7FC36A>+" + h.Points + "</color>" : "");
                WorldLabel(h.Position + Vector2.up * 1.25f, plate, Palette.HeroGlow, 16);
                Vector2 g = G(h.Position);
                float margin = 60f;
                bool off = g.x < margin || g.x > W - margin || g.y < 70f || g.y > H - margin;
                if (off)
                {
                    Vector2 c = new Vector2(W * 0.5f, H * 0.5f);
                    Vector2 d = (g - c).normalized;
                    float sx = d.x != 0f ? ((d.x > 0 ? W - ShopW - 50f : 50f) - c.x) / d.x : float.MaxValue;
                    float sy = d.y != 0f ? ((d.y > 0 ? H - 50f : 100f) - c.y) / d.y : float.MaxValue;
                    Vector2 edge = c + d * Mathf.Min(Mathf.Abs(sx), Mathf.Abs(sy));
                    Rect r = new Rect(edge.x - 30f, edge.y - 30f, 60f, 60f);
                    UI.Disc(r, h.Def.Body);
                    UI.Circle(r, h.Def.Glow, 4f);
                    GUI.Label(r, h.Name.Substring(0, 1), UI.S(26, TextAnchor.MiddleCenter, FontStyle.Bold, h.Def.Glow));
                    Vector2 tip = edge + d * 40f;
                    UI.Disc(new Rect(tip.x - 9f, tip.y - 9f, 18f, 18f), Palette.HeroGlow);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { lm.CamCtl.Focus(h.Position); lm.SelectHero(); }
                    UI.Block(r);
                }
            }

            // Placement helper near the cursor
            if (lm.Placing && !UI.PointerOverUI)
            {
                Vector2 m = UI.MouseGui;
                string text = lm.PlacingPower ? "Dragonfire: drop on orcs" : TowerData.Get(lm.PlacingType).Name + "  " + TowerData.Get(lm.PlacingType).Cost + "g";
                if (!lm.PlacementValid && !string.IsNullOrEmpty(lm.PlacementReason)) text = lm.PlacementReason;
                Color col = lm.PlacementValid ? Palette.Parchment : Palette.Bad;
                float w = text.Length * 11f + 30f;
                Rect r = new Rect(m.x + 26f, m.y + 18f, w, 34f);
                UI.Rounded(r, new Color(0f, 0f, 0f, 0.7f), 8f);
                GUI.Label(r, text, UI.S(19, TextAnchor.MiddleCenter, FontStyle.Bold, col));
            }
        }

        void DrawPopups()
        {
            foreach (var p in lm.Popups)
            {
                Vector2 g = G(p.World);
                float a = 1f - Mathf.Clamp01((p.Age - 0.8f) / 0.6f);
                Color c = p.Color;
                c.a = a;
                Rect r = new Rect(g.x - 220f, g.y - 16f, 440f, 32f);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), p.Text, UI.S(22, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0f, 0f, 0f, 0.7f * a)));
                GUI.Label(r, p.Text, UI.S(22, TextAnchor.MiddleCenter, FontStyle.Bold, c));
            }
        }

        // ================================================================== Top bar

        void DrawTopBar(float W)
        {
            Rect bar = UI.Block(new Rect(0f, 0f, W, 64f));
            UI.Fill(bar, new Color(0.07f, 0.055f, 0.045f, 0.94f));
            UI.Fill(new Rect(0f, 62f, W, 2f), Palette.PanelBorder);

            float x = 24f;
            float pulse = lm.GoldPulse;
            Rect coin = new Rect(x - 3f * pulse, 17f - 3f * pulse, 30f + 6f * pulse, 30f + 6f * pulse);
            UI.Disc(coin, Palette.Gold);
            UI.Circle(new Rect(x + 6f, 23f, 18f, 18f), Palette.EmberDeep, 2f);
            if (Event.current.type == EventType.Repaint) lm.GoldIconGui = new Vector2(x + 15f, 32f);
            GUI.Label(new Rect(x + 42f, 0f, 150f, 64f), lm.Gold.ToString(), UI.S(30 + Mathf.RoundToInt(4f * pulse), TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Gold));
            x += 180f;

            UI.Disc(new Rect(x, 17f, 30f, 30f), Palette.Bad);
            GUI.Label(new Rect(x + 42f, 0f, 160f, 64f), lm.Lives + " / " + lm.MaxLives, UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            x += 190f;

            GUI.Label(new Rect(x, 0f, 260f, 64f), "Wave " + lm.WaveNumber + " / " + lm.WaveCount, UI.S(30, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));

            GUI.Label(new Rect(W * 0.5f - 300f, 0f, 600f, 64f), lm.Level.Name.ToUpper() + "   <color=#A89A86><size=20>" + GameSession.CurrentDifficulty + "</size></color>", UI.S(28, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ember));

            bool prev = GUI.enabled;
            GUI.enabled = true;
            if (UI.Button(new Rect(W - 436f, 10f, 120f, 44f), AudioManager.Muted ? "Sound: Off" : "Sound: On", BtnKind.Ghost, 18))
                AudioManager.Muted = !AudioManager.Muted;
            GUI.enabled = prev;
            if (UI.Button(new Rect(W - 304f, 10f, 140f, 44f), lm.FastForward ? "Speed 2x [F]" : "Speed 1x [F]", lm.FastForward ? BtnKind.Accent : BtnKind.Normal, 19))
                lm.ToggleSpeed();
            if (UI.Button(new Rect(W - 152f, 10f, 128f, 44f), "Pause [Esc]", BtnKind.Normal, 19))
                lm.Pause();
        }

        // ================================================================== Shop sidebar (drag to build)

        void DrawShop(float W, float H)
        {
            Rect side = UI.Block(new Rect(W - ShopW - 8f, 72f, ShopW, H - 72f - 216f));
            UI.Panel(side);
            float x = side.x + 10f, w = side.width - 20f, y = side.y + 10f;
            GUI.Label(new Rect(x, y, w, 26f), "TOWERS  <color=#A89A86><size=15>drag onto the map</size></color>", UI.S(19, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Ember));
            y += 32f;

            for (int i = 0; i < TowerData.BuildOrder.Length; i++)
            {
                var type = TowerData.BuildOrder[i];
                var def = TowerData.Get(type);
                Rect r = new Rect(x, y, w, 104f);
                if (i == 0) archerCard = r;
                bool afford = lm.Gold >= def.Cost;
                bool active = lm.Placing && !lm.PlacingPower && lm.PlacingType == type;
                bool hover = r.Contains(Event.current.mousePosition);
                UI.Rounded(r, active ? new Color(0.35f, 0.22f, 0.12f, 1f) : hover ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f), 10f);
                UI.Outline(r, active ? Palette.Ember : hover ? Palette.Muted : Palette.PanelBorder, active ? 3f : 2f, 10f);
                TowerIcon(new Rect(r.x + 10f, r.y + 14f, 76f, 76f), def, afford);
                GUI.Label(new Rect(r.x + 96f, r.y + 10f, r.width - 100f, 28f), def.Name, UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, afford ? Palette.Parchment : Palette.Dim));
                GUI.Label(new Rect(r.x + 96f, r.y + 38f, r.width - 100f, 28f), def.Cost + " gold", UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, afford ? Palette.Gold : Palette.Bad));
                GUI.Label(new Rect(r.x + 96f, r.y + 66f, r.width - 100f, 26f), def.Role + "  [" + (i + 1) + "]", UI.S(15, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
                HandleCardPress(r, () => lm.BeginPlacement(type, true));
                if (hover && !lm.Placing)
                {
                    tipTitle = def.Name + "  (" + def.Cost + "g)";
                    tipBody = def.Blurb + "\nDamage " + def.Damage + "  ·  Range " + def.Range + "  ·  " + (1f / def.Interval).ToString("0.0") + "/s";
                    tipAnchor = r;
                    tipLeft = true;
                }
                y += 112f;
            }

            // Power-up
            y += 6f;
            GUI.Label(new Rect(x, y, w, 26f), "POWER-UP", UI.S(19, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Shard));
            y += 30f;
            Rect p = new Rect(x, y, w, 96f);
            powerCard = p;
            if (Event.current.type == EventType.Repaint) lm.PowerIconGui = new Vector2(p.x + 48f, p.center.y);
            bool hasCharge = lm.PowerCharges > 0;
            bool pActive = lm.Placing && lm.PlacingPower;
            bool pHover = p.Contains(Event.current.mousePosition);
            float pulse = lm.PowerPulse;
            UI.Rounded(p, pActive ? new Color(0.4f, 0.16f, 0.06f, 1f) : pHover ? new Color(1f, 0.5f, 0.2f, 0.12f) : new Color(1f, 0.5f, 0.2f, 0.05f), 10f);
            UI.Outline(p, pActive || pulse > 0f ? Palette.Shard : Palette.PanelBorder, pActive ? 3f : 2f + 2f * pulse, 10f);
            Rect icon = new Rect(p.x + 12f, p.y + 12f, 72f, 72f);
            UI.Disc(icon, hasCharge ? new Color(0.9f, 0.35f, 0.1f, 1f) : Palette.StoneDark);
            UI.Disc(new Rect(icon.x + 16f, icon.y + 14f, 40f, 44f), hasCharge ? Palette.Gold : Palette.Dim);
            UI.Disc(new Rect(icon.x + 26f, icon.y + 30f, 20f, 24f), hasCharge ? Color.white : Palette.StoneDark);
            GUI.Label(new Rect(p.x + 96f, p.y + 10f, p.width - 100f, 28f), "Dragonfire", UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, hasCharge ? Palette.Parchment : Palette.Dim));
            for (int i = 0; i < Balance.DragonfireMaxCharges; i++)
            {
                Rect pip = new Rect(p.x + 98f + i * 30f, p.y + 44f, 22f, 22f);
                UI.Disc(pip, i < lm.PowerCharges ? Palette.Shard : new Color(1f, 1f, 1f, 0.1f));
                UI.Circle(pip, Palette.Hex("#8A2E08"), 2f);
            }
            GUI.Label(new Rect(p.x + 96f, p.y + 68f, p.width - 100f, 22f), "drag onto orcs  [4]", UI.S(15, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
            HandleCardPress(p, () => lm.BeginPowerPlacement(true));
            if (pHover && !lm.Placing)
            {
                tipTitle = "Dragonfire (" + lm.PowerCharges + "/" + Balance.DragonfireMaxCharges + ")";
                tipBody = "A dragon sweeps over and burns the area: " + Balance.DragonfireDamage + " magic damage, burning ground for 4s, and invisible enemies revealed. Orcs drop Ember Shards that recharge it.";
                tipAnchor = p;
                tipLeft = true;
            }
            y += 104f;

            // Rockslide status (the player-triggered hazard)
            if (lm.Cliff != null && y + 74f < side.yMax)
            {
                Rect rs = new Rect(x, y + 4f, w, 66f);
                var c = lm.Cliff;
                UI.Rounded(rs, new Color(1f, 1f, 1f, 0.04f), 10f);
                Rect clock = new Rect(rs.x + 10f, rs.y + 9f, 48f, 48f);
                UI.Disc(clock, new Color(0f, 0f, 0f, 0.5f));
                UI.Clock(clock, c.RechargeFrac, c.Ready ? Palette.HeroGlow : Palette.Dim);
                string label = c.Ready ? c.DisplayName + " ready" : c.Current == Rockslide.Phase.Recharging ? c.DisplayName + " " + Mathf.CeilToInt(c.RechargeLeft) + "s" : c.DisplayName + "!";
                GUI.Label(new Rect(rs.x + 68f, rs.y + 6f, rs.width - 72f, 28f), label, UI.S(18, TextAnchor.MiddleLeft, FontStyle.Bold, c.Ready ? Palette.HeroGlow : Palette.Muted));
                GUI.Label(new Rect(rs.x + 68f, rs.y + 34f, rs.width - 72f, 24f), (lm.Marsh ? "click the dead tree" : "click the cliff") + " or press R", UI.S(15, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
                if (GUI.Button(rs, GUIContent.none, GUIStyle.none)) c.Trigger();
            }
        }

        void HandleCardPress(Rect r, System.Action begin)
        {
            var e = Event.current;
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                begin();
                e.Use();
            }
        }

        void TowerIcon(Rect r, TowerDef def, bool lit)
        {
            UI.Rounded(r, lit ? Palette.StoneDark : Palette.Hex("#3A332E"), 8f);
            UI.Rounded(new Rect(r.x + 7f, r.y + 7f, r.width - 14f, r.height - 14f), lit ? Palette.Stone : Palette.Hex("#5A524A"), 6f);
            float s = r.width * 0.5f;
            UI.Disc(new Rect(r.center.x - s * 0.5f, r.center.y - s * 0.5f, s, s), lit ? def.Roof : Palette.Hex("#4A443E"));
        }

        // ================================================================== Wave box (Bloons "play" corner)

        void DrawWaveBox(float W, float H)
        {
            Rect r = UI.Block(new Rect(W - ShopW - 8f, H - 208f, ShopW, 192f));
            UI.Panel(r, lm.WaveCountdown && lm.CanStartWave);
            float x = r.x + 12f, w = r.width - 24f;

            if (lm.WaveNumber >= lm.WaveCount && !lm.WaveCountdown)
            {
                GUI.Label(new Rect(x, r.y + 12f, w, 30f), "FINAL WAVE", UI.S(22, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Bad));
                GUI.Label(new Rect(x, r.y + 46f, w, 60f), "Orcs left: " + lm.Enemies.Count, UI.S(19, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
                waveButton = new Rect(0, 0, 0, 0);
                return;
            }

            string head;
            if (!lm.WaveCountdown) head = "Wave " + lm.WaveNumber + " attacking";
            else if (lm.WaveNumber == 0) head = "Next: Wave 1";
            else if (lm.AutoWaves) head = "Wave " + (lm.WaveNumber + 1) + " in " + Mathf.CeilToInt(lm.NextWaveTimer) + "s";
            else head = "Next: Wave " + (lm.WaveNumber + 1);
            GUI.Label(new Rect(x, r.y + 8f, w, 28f), head + (lm.NextWaveIsBoss && lm.WaveCountdown ? "  <color=#D9533B>BOSS</color>" : ""), UI.S(20, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            string preview = lm.WaveCountdown ? lm.NextWavePreview : lm.Enemies.Count + " orcs on the road";
            GUI.Label(new Rect(x, r.y + 38f, w, 62f), preview, UI.S(16, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));

            waveButton = new Rect(x, r.yMax - 76f, w, 64f);
            if (lm.WaveCountdown)
            {
                bool prev = GUI.enabled;
                GUI.enabled = prev && lm.CanStartWave;
                string btn = lm.WaveNumber == 0 || !lm.AutoWaves ? "START WAVE  [Space]" : lm.EarlyBonus > 0 ? "CALL EARLY  +" + lm.EarlyBonus + "g" : "CALL EARLY  [Space]";
                if (UI.Button(waveButton, btn, BtnKind.Accent, 21)) lm.StartNextWave(true);
                GUI.enabled = prev;
            }
            else
            {
                UI.Rounded(waveButton, new Color(0f, 0f, 0f, 0.35f), 10f);
                GUI.Label(waveButton, "Wave in progress", UI.S(19, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim));
            }
        }

        // ================================================================== Hero card

        void DrawHeroCard(float H)
        {
            var h = lm.HeroUnit;
            Rect r = UI.Block(new Rect(16f, H - 196f, 500f, 180f));
            UI.Panel(r, lm.HeroSelected);

            // Portrait (click to select / find him)
            Rect portrait = new Rect(r.x + 14f, r.y + 14f, 112f, 112f);
            heroPortrait = portrait;
            DrawHeroFace(portrait, h.Def, h.Dead);
            if (h.Dead)
            {
                UI.Clock(portrait, h.RespawnFrac, new Color(1f, 0.82f, 0.35f, 0.55f));
                GUI.Label(portrait, Mathf.CeilToInt(h.RespawnLeft) + "s", UI.S(34, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment));
            }
            // Level badge
            Rect lvl = new Rect(portrait.xMax - 34f, portrait.yMax - 34f, 40f, 40f);
            UI.Disc(lvl, Palette.Ink);
            UI.Circle(lvl, Palette.HeroGlow, 3f);
            GUI.Label(lvl, h.Level.ToString(), UI.S(20, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.HeroGlow));
            if (h.Points > 0)
            {
                float pl = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                Rect pt = new Rect(portrait.x - 4f, portrait.y - 4f, 40f, 40f);
                UI.Disc(pt, Color.Lerp(Palette.Good, Color.white, pl * 0.3f));
                GUI.Label(pt, "+" + h.Points, UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ink));
            }
            if (GUI.Button(portrait, GUIContent.none, GUIStyle.none))
            {
                lm.SelectHero();
                lm.CamCtl.Focus(h.Dead ? h.RespawnPoint : h.Position);
            }
            GUI.Label(new Rect(portrait.x - 6f, portrait.yMax + 6f, portrait.width + 12f, 22f), "click / [H]", UI.S(15, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Dim));

            float x = r.x + 140f, w = 230f;
            GUI.Label(new Rect(x, r.y + 10f, w + 120f, 28f), h.Name, UI.S(23, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            // XP bar
            UI.Bar(new Rect(x, r.y + 42f, w, 10f), h.XpIntoLevel, Palette.HeroGlow, new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(x, r.y + 52f, w, 20f), h.Level >= HeroData.MaxLevel ? "Max level" : "Level " + h.Level + "  ·  XP " + h.Xp + " / " + HeroData.LevelXp[h.Level], UI.S(14, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
            if (h.Dead)
            {
                GUI.Label(new Rect(x, r.y + 80f, w, 50f), "Recovering at the gate: back in " + Mathf.CeilToInt(h.RespawnLeft) + "s", UI.S(18, TextAnchor.UpperLeft, FontStyle.Bold, Palette.Bad, true));
            }
            else
            {
                UI.Bar(new Rect(x, r.y + 80f, w, 18f), h.Hp / h.MaxHp, h.Hp / h.MaxHp > 0.35f ? Palette.Good : Palette.Bad, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(new Rect(x, r.y + 80f, w, 18f), Mathf.CeilToInt(h.Hp) + " / " + Mathf.CeilToInt(h.MaxHp), UI.S(14, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment));
                string status = h.Moving ? "Moving" : h.Ranged ? (h.RangedTarget != null ? "Shooting" : "Watching the road") : h.BlockingCount > 0 ? "Blocking " + h.BlockingCount + " / " + h.MaxBlock : "Holding position";
                if (h.Invulnerable) status = "LAST STAND!";
                GUI.Label(new Rect(x, r.y + 102f, w, 22f), status, UI.S(17, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));
            }
            GUI.Label(new Rect(x, r.y + 130f, w, 40f), h.Points > 0 ? "<color=#7FC36A>Hero point ready!</color> Spend it in the tree." : "RMB: move  ·  click portrait: skill tree", UI.S(15, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Dim, true));

            // Shield Slam: big round button with a radial cooldown clock
            Rect ab = new Rect(r.xMax - 118f, r.y + 20f, 100f, 100f);
            slamButton = ab;
            bool ready = h.AbilityReady;
            float rp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            if (ready) UI.Disc(new Rect(ab.x - 8f * rp, ab.y - 8f * rp, ab.width + 16f * rp, ab.height + 16f * rp), new Color(1f, 0.85f, 0.4f, 0.25f));
            UI.Disc(ab, ready ? Palette.Hex("#6A4A18") : Palette.Hex("#2A221D"));
            if (!ready && !h.Dead) UI.Clock(new Rect(ab.x + 4f, ab.y + 4f, ab.width - 8f, ab.height - 8f), 1f - h.AbilityLeft / h.AbilityCooldown, new Color(1f, 0.82f, 0.35f, 0.45f));
            UI.Circle(ab, ready ? Palette.HeroGlow : Palette.PanelBorder, 4f);
            UI.Disc(new Rect(ab.center.x - 22f, ab.center.y - 26f, 44f, 36f), ready ? Palette.Hex("#D9C27A") : Palette.Hex("#5A524A"));
            GUI.Label(new Rect(ab.x, ab.y + 54f, ab.width, 36f), ready ? "Q" : h.Dead ? "-" : Mathf.CeilToInt(h.AbilityLeft) + "s", UI.S(ready ? 22 : 26, TextAnchor.MiddleCenter, FontStyle.Bold, ready ? Palette.Parchment : Palette.Muted));
            GUI.Label(new Rect(ab.x - 24f, ab.yMax + 4f, ab.width + 48f, 22f), ready ? "<color=#FFD36A>" + h.Def.AbilityName.ToUpper() + "</color>" : h.Def.AbilityName, UI.S(14, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Muted));
            if (GUI.Button(ab, GUIContent.none, GUIStyle.none)) h.UseAbility();
            if (ab.Contains(Event.current.mousePosition))
            {
                tipTitle = h.Def.AbilityName + "  [Q]";
                tipBody = (h.Ranged ? "Rains arrows on the enemy she's aiming at (within " + h.Range.ToString("0.0") + " units): " : "Hits every enemy around him: ") +
                          h.AbilityDamage.ToString("0") + " damage and a " + h.AbilityStun.ToString("0.#") + "s stun in a " + h.AbilityRadius.ToString("0.0") + "-unit radius. Stuns INTERRUPT boss wind-ups. Cooldown " + h.AbilityCooldown.ToString("0") + "s.";
                tipAnchor = ab;
                tipLeft = false;
            }
        }

        // ================================================================== Docked panels

        Rect DockRect(Vector2 worldPos, float W, float H)
        {
            Vector2 sp = G(worldPos);
            bool dockLeft = sp.x > (W - ShopW) * 0.5f;
            float h = H - 76f - 216f;
            return new Rect(dockLeft ? 16f : W - PanelW - 16f, 76f, PanelW, h);
        }

        void DrawTowerPanel(float W, float H)
        {
            var tw = lm.SelectedTower;
            if (tw == null) return;
            Rect r = DockRect(tw.Position, W, H);
            UI.Block(r);
            UI.Panel(r, true);
            float x = r.x + 16f, w = r.width - 32f, y = r.y + 12f;

            GUI.Label(new Rect(x, y, w - 50f, 32f), tw.Def.Name, UI.S(26, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            if (UI.Button(new Rect(r.xMax - 52f, y, 36f, 36f), "X", BtnKind.Ghost, 20)) { lm.ClearSelection(); return; }
            y += 34f;
            GUI.Label(new Rect(x, y, w, 22f), tw.Kills + " orcs defeated  ·  invested " + tw.Invested + "g", UI.S(16, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim));
            y += 28f;

            TowerIcon(new Rect(x, y, 70f, 70f), tw.Def, true);
            string kind = tw.Kind == DamageKind.Magic ? "magic" : tw.Kind == DamageKind.True ? "armor-piercing" : "physical";
            GUI.Label(new Rect(x + 82f, y, w - 82f, 24f), "Damage <b>" + tw.Damage.ToString("0.#") + "</b> " + kind, UI.S(17, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));
            GUI.Label(new Rect(x + 82f, y + 22f, w - 82f, 24f), (1f / tw.Interval).ToString("0.00") + " attacks/s  ·  Range <b>" + tw.EffectiveRange.ToString("0.0") + "</b>", UI.S(17, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));
            var specials = tw.SpecialLines();
            GUI.Label(new Rect(x + 82f, y + 44f, w - 82f, 30f), specials.Count > 0 ? string.Join(" · ", specials.ToArray()) : "No specials yet", UI.S(14, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Dim, true));
            y += 78f;

            string status = null;
            Color sc = Palette.Bad;
            if (tw.Frozen) { status = "FROZEN by a Banshee's wail: " + tw.DisabledLeft.ToString("0.0") + "s"; sc = Palette.Frost; }
            else if (tw.Disabled) status = "KNOCKED OUT: " + tw.DisabledLeft.ToString("0.0") + "s";
            else if (tw.InAshStorm) { status = "In the " + (lm.Storm != null ? lm.Storm.DisplayName : "storm") + ": -40% range"; sc = Palette.Muted; }
            else if (tw.SlowedByDrummer) { status = "Slowed 30% by a War Drummer"; sc = Palette.Ember; }
            else if (tw.Rallied) { status = "RALLIED: +30% attack speed"; sc = Palette.HeroGlow; }
            else if (tw.Overcharged) { status = "OVERCHARGED: double speed"; sc = Palette.Magic; }
            if (status != null)
            {
                Rect sr = new Rect(x, y, w, 28f);
                UI.Rounded(sr, new Color(sc.r, sc.g, sc.b, 0.18f), 8f);
                GUI.Label(sr, status, UI.S(16, TextAnchor.MiddleCenter, FontStyle.Bold, sc));
            }
            y += 34f;

            // Targeting priority (Bloons style)
            GUI.Label(new Rect(x, y, 110f, 44f), "Targeting", UI.S(18, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            if (UI.Button(new Rect(x + 112f, y + 2f, 44f, 40f), "<", BtnKind.Normal, 20)) tw.CyclePriority(-1);
            Rect pr = new Rect(x + 160f, y + 2f, w - 212f, 40f);
            UI.Rounded(pr, new Color(0f, 0f, 0f, 0.35f), 8f);
            GUI.Label(pr, TowerData.PriorityName(tw.Priority), UI.S(20, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Gold));
            if (UI.Button(new Rect(x + w - 48f, y + 2f, 44f, 40f), ">", BtnKind.Normal, 20)) tw.CyclePriority(1);
            y += 52f;

            // Three upgrade paths, stacked
            float rowH = Mathf.Min(140f, (r.yMax - y - 76f) / 3f);
            for (int b = 0; b < 3; b++)
            {
                Rect row = new Rect(x, y, w, rowH - 8f);
                DrawUpgradeRow(tw, b, row);
                y += rowH;
            }

            if (UI.Button(new Rect(x, r.yMax - 64f, w, 50f), "Sell  +" + tw.SellValue + "g", BtnKind.Danger, 20)) lm.Sell(tw);
        }

        void DrawUpgradeRow(Tower tw, int b, Rect row)
        {
            Color bc = Palette.Branch(b);
            UI.Rounded(row, new Color(1f, 1f, 1f, 0.04f), 10f);
            UI.Fill(new Rect(row.x, row.y + 8f, 4f, row.height - 16f), bc);
            int level = tw.Levels[b];
            GUI.Label(new Rect(row.x + 14f, row.y + 6f, 120f, 24f), TowerData.BranchNames[b].ToUpper(), UI.S(16, TextAnchor.MiddleLeft, FontStyle.Bold, bc));
            for (int i = 0; i < 3; i++)
                UI.Disc(new Rect(row.x + 100f + i * 22f, row.y + 11f, 14f, 14f), i < level ? bc : new Color(1f, 1f, 1f, 0.12f));

            Rect btn = new Rect(row.xMax - 116f, row.y + 34f, 106f, row.height - 44f);
            if (level >= 3)
            {
                GUI.Label(new Rect(row.x + 14f, row.y + 32f, row.width - 28f, row.height - 40f), "<b>" + tw.Def.Upgrades[b, 2].Name + "</b>\nPath complete.", UI.S(15, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
                GUI.Label(btn, "MAXED", UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, bc));
                return;
            }
            var up = tw.Def.Upgrades[b, level];
            GUI.Label(new Rect(row.x + 14f, row.y + 32f, row.width - 140f, row.height - 38f), "<b>" + up.Name + "</b>\n" + up.Desc, UI.S(15, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            string reason;
            bool allowed = tw.CanUpgrade(b, out reason);
            int cost = tw.NextCost(b);
            if (!allowed)
            {
                UI.Rounded(btn, new Color(0f, 0f, 0f, 0.35f), 8f);
                GUI.Label(btn, "Path\nclosed", UI.S(16, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim));
                return;
            }
            bool afford = lm.Gold >= cost;
            bool prev = GUI.enabled;
            GUI.enabled = prev && afford;
            if (UI.Button(btn, "BUY\n" + cost + "g", BtnKind.Accent, 18)) lm.TryUpgrade(tw, b);
            GUI.enabled = prev;
            if (!afford) GUI.Label(new Rect(btn.x, btn.yMax - 4f, btn.width, 16f), "need " + (cost - lm.Gold) + "g", UI.S(13, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Bad));
        }

        void DrawHeroPanel(float W, float H)
        {
            var h = lm.HeroUnit;
            Rect r = DockRect(h.Dead ? h.RespawnPoint : h.Position, W, H);
            UI.Block(r);
            UI.Panel(r, true);
            float x = r.x + 16f, w = r.width - 32f, y = r.y + 12f;

            GUI.Label(new Rect(x, y, w - 50f, 32f), h.Name + "  <color=#FFD36A>Lv " + h.Level + "</color>", UI.S(26, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            if (UI.Button(new Rect(r.xMax - 52f, y, 36f, 36f), "X", BtnKind.Ghost, 20)) { lm.ClearSelection(); return; }
            y += 38f;
            UI.Bar(new Rect(x, y, w, 12f), h.XpIntoLevel, Palette.HeroGlow, new Color(0f, 0f, 0f, 0.6f));
            y += 16f;
            GUI.Label(new Rect(x, y, w, 22f), h.Level >= HeroData.MaxLevel ? "Max level" : "XP " + h.Xp + " / " + HeroData.LevelXp[h.Level] + "  (every enemy defeated; double for the hero's own kills)", UI.S(14, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Dim, true));
            y += 30f;

            Rect pts = new Rect(x, y, w, 40f);
            UI.Rounded(pts, h.Points > 0 ? new Color(0.5f, 0.76f, 0.42f, 0.2f) : new Color(1f, 1f, 1f, 0.04f), 8f);
            GUI.Label(pts, h.Points > 0 ? "Hero points to spend: <b>" + h.Points + "</b>" : "Earn hero points by leveling up", UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, h.Points > 0 ? Palette.Good : Palette.Dim));
            y += 48f;

            string stats = "Health " + Mathf.CeilToInt(h.MaxHp) + "  ·  Damage " + h.Damage.ToString("0") + (h.Kind == DamageKind.True ? " (ignores armor)" : "") +
                           (h.Ranged ? "  ·  Range " + h.Range.ToString("0.0") : "  ·  Blocks " + h.MaxBlock) +
                           "\n" + h.Def.AbilityName + " " + h.AbilityRadius.ToString("0.0") + "u, " + h.AbilityStun.ToString("0.#") + "s stun, " + h.AbilityCooldown.ToString("0") + "s cooldown";
            GUI.Label(new Rect(x, y, w, 46f), stats, UI.S(15, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            y += 52f;

            float rowH = Mathf.Min(140f, (r.yMax - y - 16f) / 3f);
            for (int b = 0; b < 3; b++)
            {
                Rect row = new Rect(x, y + b * rowH, w, rowH - 8f);
                Color bc = Palette.Branch(b);
                UI.Rounded(row, new Color(1f, 1f, 1f, 0.04f), 10f);
                UI.Fill(new Rect(row.x, row.y + 8f, 4f, row.height - 16f), bc);
                int level = h.Tree[b];
                GUI.Label(new Rect(row.x + 14f, row.y + 6f, 120f, 24f), h.Def.BranchNames[b].ToUpper(), UI.S(16, TextAnchor.MiddleLeft, FontStyle.Bold, bc));
                for (int i = 0; i < 3; i++)
                    UI.Disc(new Rect(row.x + 110f + i * 22f, row.y + 11f, 14f, 14f), i < level ? bc : new Color(1f, 1f, 1f, 0.12f));
                Rect btn = new Rect(row.xMax - 116f, row.y + 34f, 106f, row.height - 44f);
                if (level >= 3)
                {
                    GUI.Label(new Rect(row.x + 14f, row.y + 32f, row.width - 28f, row.height - 40f), "<b>" + h.Def.Tree[b, 2].Name + "</b>\nBranch mastered.", UI.S(15, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
                    GUI.Label(btn, "MAXED", UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, bc));
                    continue;
                }
                var up = h.Def.Tree[b, level];
                GUI.Label(new Rect(row.x + 14f, row.y + 32f, row.width - 140f, row.height - 38f), "<b>" + up.Name + "</b>\n" + up.Desc, UI.S(15, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
                bool prev = GUI.enabled;
                GUI.enabled = prev && h.CanLearn(b);
                if (UI.Button(btn, "LEARN\n1 point", BtnKind.Accent, 17)) { h.Learn(b); if (h.Points == 0) lm.DismissLevelUp(); }
                GUI.enabled = prev;
            }
        }

        // ================================================================== Objectives, boss bar, toasts

        /// <summary>Tutorial objective card, docked right beside the hero card so it never covers the road.</summary>
        float DrawObjectives(float x, float right, float H)
        {
            var tut = lm.Tutorial;
            if (tut == null || !tut.Ready) return x;
            float w = Mathf.Clamp(right - x, 360f, 560f);
            Rect r = UI.Block(new Rect(x, H - 196f, w, 180f));
            float flash = tut.FlashAmount;
            UI.Rounded(r, new Color(0.08f + 0.2f * flash, 0.06f + 0.15f * flash, 0.04f, 0.95f), 12f);
            UI.Outline(r, Palette.HeroGlow, 3f, 12f);
            GUI.Label(new Rect(r.x + 18f, r.y + 8f, w - 36f, 24f), "TRAINING  " + Mathf.Min(tut.Step + 1, tut.StepCount) + " / " + tut.StepCount, UI.S(16, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Dim));
            UI.Bar(new Rect(r.x + 150f, r.y + 16f, w - 168f, 8f), Mathf.Clamp01((float)tut.Step / tut.StepCount), Palette.HeroGlow, new Color(0f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(r.x + 18f, r.y + 34f, w - 36f, 30f), tut.Title, UI.S(22, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.HeroGlow));
            GUI.Label(new Rect(r.x + 18f, r.y + 68f, w - 36f, r.height - 74f), tut.Body, UI.S(17, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Parchment, true));
            return r.xMax + 16f;
        }

        /// <summary>
        /// Level-up panel: says what leveling gives (1 hero point), what the next upgrade in each branch does,
        /// and lets the player learn it in one click. Stays until the points are spent or "Later".
        /// </summary>
        float DrawLevelUpPanel(float x, float right, float H)
        {
            var h = lm.HeroUnit;
            if (!lm.LevelUpPanelOpen || h.Points <= 0 || lm.HeroSelected) return x;
            float w = 440f, ph = 180f;
            float y = H - 196f, origX = x;
            bool inRow = right - x >= w;
            if (!inRow)
            {
                // No room in the row: sit above the hero card instead.
                x = 16f;
                y = H - 196f - ph - 12f;
                w = 500f;
            }
            Rect r = UI.Block(new Rect(x, y, w, ph));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            UI.Rounded(r, new Color(0.07f, 0.09f, 0.05f, 0.96f), 12f);
            UI.Outline(r, Color.Lerp(Palette.HeroGlow, Palette.Good, pulse), 3f, 12f);
            GUI.Label(new Rect(r.x + 16f, r.y + 8f, w - 60f, 30f), h.Name + " reached <color=#FFD36A>Level " + h.Level + "</color>!", UI.S(22, TextAnchor.MiddleLeft, FontStyle.Bold, Palette.Parchment));
            string sub = "+1 hero point" + (h.Points > 1 ? " (" + h.Points + " to spend)" : "") + ". Pick an upgrade:";
            if (h.Level < HeroData.MaxLevel) sub += "  <color=#7A6E62>next level in " + h.XpToNext + " XP</color>";
            GUI.Label(new Rect(r.x + 16f, r.y + 38f, w - 32f, 22f), sub, UI.S(15, TextAnchor.MiddleLeft, FontStyle.Normal, Palette.Muted));

            bool prevE = GUI.enabled;
            if (GUI.Button(new Rect(r.xMax - 40f, r.y + 8f, 30f, 28f), "x", UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim))) { lm.DismissLevelUp(); return origX; }

            float bw = (w - 32f - 16f) / 3f;
            for (int b = 0; b < 3; b++)
            {
                Rect btn = new Rect(r.x + 16f + b * (bw + 8f), r.y + 66f, bw, 70f);
                Color bc = Palette.Branch(b);
                int lvl = h.Tree[b];
                bool can = h.CanLearn(b);
                bool hover = can && btn.Contains(Event.current.mousePosition);
                UI.Rounded(btn, hover ? new Color(bc.r * 0.45f, bc.g * 0.45f, bc.b * 0.45f, 1f) : new Color(1f, 1f, 1f, can ? 0.07f : 0.03f), 8f);
                UI.Outline(btn, can ? bc : Palette.PanelBorder, 2f, 8f);
                GUI.Label(new Rect(btn.x + 8f, btn.y + 4f, btn.width - 16f, 20f), h.Def.BranchNames[b].ToUpper() + "  " + lvl + "/3", UI.S(13, TextAnchor.MiddleLeft, FontStyle.Bold, bc));
                string nm = lvl >= 3 ? "Mastered" : h.Def.Tree[b, lvl].Name;
                GUI.Label(new Rect(btn.x + 8f, btn.y + 24f, btn.width - 16f, 42f), nm, UI.S(16, TextAnchor.UpperLeft, FontStyle.Bold, can ? Palette.Parchment : Palette.Dim, true));
                if (lvl < 3 && btn.Contains(Event.current.mousePosition))
                {
                    tipTitle = h.Def.Tree[b, lvl].Name + "  (" + h.Def.BranchNames[b] + " " + (lvl + 1) + "/3)";
                    tipBody = h.Def.Tree[b, lvl].Desc + "\nClick to learn for 1 hero point.";
                    tipAnchor = btn;
                    tipLeft = false;
                }
                GUI.enabled = prevE && can;
                if (GUI.Button(btn, GUIContent.none, GUIStyle.none))
                {
                    AudioManager.Play(Sfx.Click, 0.6f);
                    h.Learn(b);
                    if (h.Points == 0) lm.DismissLevelUp();
                }
                GUI.enabled = prevE;
            }

            float by = r.y + 142f;
            if (UI.Button(new Rect(r.x + 16f, by, 170f, 30f), "Full tree  [H]", BtnKind.Ghost, 16)) lm.SelectHero();
            if (UI.Button(new Rect(r.xMax - 126f, by, 110f, 30f), "Later", BtnKind.Ghost, 16)) lm.DismissLevelUp();
            return inRow ? r.xMax + 16f : origX;
        }

        /// <summary>Simple portrait for a hero (card, level-up panel, hero select).</summary>
        public static void DrawHeroFace(Rect portrait, HeroDef d, bool dead)
        {
            UI.Disc(portrait, dead ? Palette.StoneDark : d.Body);
            UI.Circle(portrait, d.Glow, 4f);
            float s = portrait.width / 112f;
            if (d.Ranged)
            {
                UI.Disc(new Rect(portrait.x + 28f * s, portrait.y + 18f * s, 56f * s, 56f * s), dead ? Palette.Dim : d.Cloak);
                UI.Disc(new Rect(portrait.x + 38f * s, portrait.y + 30f * s, 36f * s, 36f * s), dead ? Palette.Dim : Palette.Hex("#E9C9A4"));
                UI.Rounded(new Rect(portrait.x + 24f * s, portrait.y + 76f * s, 64f * s, 14f * s), d.Trim, 6f * s);
            }
            else
            {
                UI.Disc(new Rect(portrait.x + 33f * s, portrait.y + 24f * s, 46f * s, 46f * s), dead ? Palette.Dim : Palette.Hex("#D8D2CA"));
                UI.Rounded(new Rect(portrait.x + 28f * s, portrait.y + 74f * s, 56f * s, 18f * s), d.Cloak, 6f * s);
            }
        }

        void DrawBossBar(float W)
        {
            var b = lm.Boss;
            if (b == null || !b.Alive) return;
            float w = Mathf.Clamp(W - ShopW - 2f * 420f, 420f, 820f);
            float y = 76f;
            Rect r = UI.Block(new Rect((W - ShopW - w) * 0.5f, y, w, b.WindingUp ? 116f : 84f));
            var gorruk = b as BossGorruk;
            bool enraged = gorruk != null && gorruk.Enraged;
            UI.Panel(r, b.WindingUp || enraged);
            string title = b.Def.Name.ToUpper() + "   <color=#A89A86><size=17>" + b.Def.Mechanic + "</size></color>";
            if (enraged) title += "  <color=#D9533B>ENRAGED</color>";
            if (b.Staggered) title += "  <color=#8FD8FF>STAGGERED +25%</color>";
            GUI.Label(new Rect(r.x + 16f, r.y + 6f, w - 32f, 32f), title, UI.S(21, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment));
            Rect hp = new Rect(r.x + 20f, r.y + 44f, w - 40f, 22f);
            UI.Bar(hp, b.Hp / b.MaxHp, Palette.Bad, new Color(0f, 0f, 0f, 0.6f));
            if (gorruk != null) UI.Fill(new Rect(hp.x + hp.width * 0.5f - 1f, hp.y - 4f, 2f, hp.height + 8f), Palette.Gold);
            GUI.Label(hp, Mathf.CeilToInt(b.Hp) + " / " + Mathf.CeilToInt(b.MaxHp), UI.S(15, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment));
            if (b.WindingUp)
            {
                Rect wr = new Rect(r.x + 20f, r.y + 76f, w - 40f, 28f);
                UI.Bar(wr, b.WindUpProgress, Palette.Interrupt, new Color(0f, 0f, 0f, 0.6f));
                bool flash = Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f;
                GUI.Label(wr, b.WindUpName + " CHARGING: STUN HIM TO INTERRUPT!", UI.S(16, TextAnchor.MiddleCenter, FontStyle.Bold, flash ? Palette.Ink : Palette.Parchment));
            }
        }

        /// <summary>Advisor toasts: compact, bottom-center, between the hero card and the shop.</summary>
        float DrawToasts(float rowX, float right, float W, float H)
        {
            if (lm.Announcements.Count == 0) return rowX;
            var a = lm.Announcements[0];
            float fadeIn = Mathf.Clamp01(a.Age / 0.25f);
            float fadeOut = Mathf.Clamp01((a.Duration - a.Age) / 0.5f);
            float alpha = Mathf.Min(fadeIn, fadeOut);
            float h = 100f;
            float left = rowX, w = Mathf.Min(680f, right - left);
            float y = H - 52f - h;
            if (w < 320f)
            {
                // Row is full (tutorial card + level-up panel): stack the toast above them.
                left = 532f; w = Mathf.Min(680f, right - left);
                if (w < 320f) { left = 16f; w = Mathf.Min(680f, right - 16f); }
                y = H - 196f - h - 12f;
            }
            else if (rowX <= 532f) left = rowX + (right - rowX - w) * 0.5f;
            bool inRow = w >= 320f && y == H - 52f - h;
            Rect r = new Rect(left, y, w, h);
            UI.Block(r);

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UI.Rounded(r, new Color(0.07f, 0.055f, 0.045f, 0.93f), 12f);
            UI.Outline(r, a.Accent, 2f, 12f);
            Rect face = new Rect(r.x + 12f, r.y + 14f, 56f, 56f);
            UI.Disc(face, Palette.Hex("#3F6E8F"));
            UI.Disc(new Rect(face.x + 16f, face.y + 8f, 24f, 24f), Palette.Hex("#E3C4A0"));
            UI.Circle(face, a.Accent, 3f);
            GUI.Label(new Rect(r.x + 82f, r.y + 6f, w - 130f, 28f), a.Title, UI.S(19, TextAnchor.MiddleLeft, FontStyle.Bold, a.Accent));
            GUI.Label(new Rect(r.x + 82f, r.y + 34f, w - 96f, h - 38f), a.Body, UI.S(16, TextAnchor.UpperLeft, FontStyle.Normal, Palette.Muted, true));
            if (lm.Announcements.Count > 1)
                GUI.Label(new Rect(r.xMax - 60f, r.y + 6f, 50f, 24f), "+" + (lm.Announcements.Count - 1), UI.S(15, TextAnchor.MiddleRight, FontStyle.Bold, Palette.Dim));
            bool prevE = GUI.enabled;
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.xMax - 34f, r.yMax - 30f, 28f, 26f), "x", UI.S(16, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Dim)))
                a.Age = Mathf.Max(a.Age, a.Duration - 0.3f);
            GUI.enabled = prevE;
            GUI.color = prevColor;
            return inRow && rowX > 532f ? r.xMax + 16f : rowX;
        }

        void DrawHint(float left, float right, float H)
        {
            string text;
            if (lm.Placing) text = lm.PlacingPower ? "Drop Dragonfire on the orcs.  <b>RMB / Esc</b> cancel" : "Drop the tower beside the road.  <b>RMB / Esc</b> cancel";
            else if (lm.HeroSelected) text = "<b>" + lm.HeroUnit.Name + " selected:</b> click the ground or right-click to move";
            else text = "<b>Drag</b> towers from the shop   <b>RMB</b> move hero   <b>Q</b> " + lm.HeroUnit.Def.AbilityName + "   <b>4</b> Dragonfire   <b>R</b> " + (lm.Cliff != null ? lm.Cliff.DisplayName : "Rockslide") + "   <b>WASD</b> pan   <b>M</b> mute";
            if (right - left < 420f) return;
            GUI.Label(new Rect(left, H - 46f, right - left, 30f), text, UI.S(16, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Dim, true));
        }

        void DrawTutorialPointer()
        {
            var tut = lm.Tutorial;
            if (tut == null || !tut.Ready || tut.Finished) return;
            Rect target;
            string label;
            switch (tut.Target)
            {
                case TutorialDirector.Focus.ShopArcher: target = archerCard; label = "Drag me onto the map!"; break;
                case TutorialDirector.Focus.WaveButton: target = waveButton; label = "Click to start"; break;
                case TutorialDirector.Focus.SlamButton: target = slamButton; label = "Press Q"; break;
                case TutorialDirector.Focus.PowerCard: target = powerCard; label = "Drag onto orcs!"; break;
                case TutorialDirector.Focus.Tower:
                    if (lm.SelectedTower != null || lm.Towers.Count == 0) return;
                    Vector2 g = G(lm.Towers[0].Position);
                    target = new Rect(g.x - 40f, g.y - 40f, 80f, 80f);
                    label = "Click your tower";
                    break;
                default: return;
            }
            if (target.width <= 0f) return;
            float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            Rect o = new Rect(target.x - 6f - 4f * p, target.y - 6f - 4f * p, target.width + 12f + 8f * p, target.height + 12f + 8f * p);
            UI.Outline(o, Palette.HeroGlow, 4f, 14f);
            float lw = label.Length * 11f + 30f;
            bool leftSide = target.x > UI.W * 0.5f;
            Rect lr = new Rect(leftSide ? o.x - lw - 12f : o.xMax + 12f, target.center.y - 18f, lw, 36f);
            UI.Rounded(lr, Palette.HeroGlow, 10f);
            GUI.Label(lr, label, UI.S(18, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Ink));
        }

        // ================================================================== Overlays

        void DrawPause(float W, float H)
        {
            UI.Veil(0.6f);
            float w = 560f, h = 640f;
            Rect r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            UI.Panel(r, true);
            UI.Shadowed(new Rect(r.x, r.y + 20f, w, 60f), "Paused", UI.S(52, TextAnchor.MiddleCenter, FontStyle.Bold, Palette.Parchment), 3f);
            float bx = r.x + 60f, bw = w - 120f, y = r.y + 100f;
            if (UI.Button(new Rect(bx, y, bw, 60f), "Resume", BtnKind.Accent)) lm.Resume();
            y += 72f;
            if (UI.Button(new Rect(bx, y, bw, 60f), "Restart Level")) lm.Restart();
            y += 72f;
            if (UI.Button(new Rect(bx, y, bw, 60f), "Level Select")) lm.ToLevelSelect();
            y += 72f;
            if (UI.Button(new Rect(bx, y, bw, 60f), "Title Screen")) lm.ToTitle();
            y += 84f;
            float mv = AudioManager.MusicVolume;
            float nmv = UI.Slider(new Rect(bx, y, bw, 40f), "Music", mv);
            if (!Mathf.Approximately(mv, nmv)) AudioManager.MusicVolume = nmv;
            y += 48f;
            float sv = AudioManager.SfxVolume;
            float nsv = UI.Slider(new Rect(bx, y, bw, 40f), "Effects", sv);
            if (!Mathf.Approximately(sv, nsv)) AudioManager.SfxVolume = nsv;
            y += 52f;
            if (UI.Button(new Rect(bx, y, bw, 44f), AudioManager.Muted ? "Sound: Off  [M]" : "Sound: On  [M]", BtnKind.Ghost, 20)) AudioManager.Muted = !AudioManager.Muted;
            GUI.Label(new Rect(r.x + 20f, r.yMax - 40f, w - 40f, 30f), "Debug: F9 = +500 gold   ·   F10 = skip to the boss wave", UI.S(16, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Dim));
        }

        void DrawEnd(float W, float H)
        {
            bool won = lm.State == GameState.Victory;
            UI.Veil(0.65f);
            float w = 680f, h = 560f;
            Rect r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            UI.Panel(r, true);
            string title = won ? (lm.Level.IsTutorial ? "TRAINING COMPLETE" : "VICTORY") : "THE BASTION HAS FALLEN";
            UI.Shadowed(new Rect(r.x, r.y + 26f, w, 70f), title, UI.S(won ? 56 : 44, TextAnchor.MiddleCenter, FontStyle.Bold, won ? Palette.Gold : Palette.Bad), 3f);
            string sub = won ? (lm.Level.IsTutorial ? "You're ready, Commander. The Ashen Pass awaits." : "The Ashen Pass holds. Gorruk's Warhost is broken.") : "The orcs broke through the gate. Rebuild and try again, Commander.";
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
            string stats = "Lives left: <b>" + lm.Lives + " / " + lm.MaxLives + "</b>     Waves: <b>" + lm.WaveNumber + " / " + lm.WaveCount + "</b>\nOrcs defeated: <b>" + lm.Kills + "</b>     " + lm.HeroUnit.Name + ": <b>Lv " + lm.HeroUnit.Level + "</b>     Time: <b>" + mins + ":" + secs.ToString("00") + "</b>";
            GUI.Label(new Rect(r.x + 40f, r.y + 272f, w - 80f, 70f), stats, UI.S(21, TextAnchor.MiddleCenter, FontStyle.Normal, Palette.Parchment, true));

            float bw = (w - 80f - 24f) / 3f, y = r.yMax - 110f;
            if (UI.Button(new Rect(r.x + 40f, y, bw, 64f), won ? "Play Again" : "Try Again", BtnKind.Accent, 22)) lm.Restart();
            if (UI.Button(new Rect(r.x + 40f + bw + 12f, y, bw, 64f), "Level Select", BtnKind.Normal, 22)) lm.ToLevelSelect();
            if (UI.Button(new Rect(r.x + 40f + 2f * (bw + 12f), y, bw, 64f), "Title Screen", BtnKind.Normal, 22)) lm.ToTitle();
        }
    }
}

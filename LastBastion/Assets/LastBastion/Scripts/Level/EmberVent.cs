using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Hazard: the Ember Vent, a crack in the Ashen Pass road.
    /// Unique mechanic: it is NEUTRAL. On a fixed, telegraphed cycle (glow, pulse, then eruption) it
    /// burns everything standing in it: orcs AND Sir Aldric. Smart players park the hero just past
    /// the vent so blocked orcs stand in the fire, then step clear before it blows.
    /// </summary>
    public class EmberVent : MonoBehaviour
    {
        public enum Phase { Dormant, Warning, Erupting }

        public const float Radius = 1.5f;
        public const float DormantTime = 9f;
        public const float WarningTime = 2.5f;
        public const float EruptTime = 2.5f;
        public const float Dps = 32f;

        public Phase Current { get; private set; }
        public float TimeLeft => timer;
        public Vector2 Position => transform.position;
        /// <summary>"Ember Vent" on the Ashen Pass, "Marsh Gas" in the Weeping Marsh.</summary>
        public string DisplayName { get; private set; }

        float timer = 7f;
        // Theme colors (Ashen: fire. Marsh: green swamp gas.)
        Color coreIdle, coreHot, flameA, flameB, smoke;
        float fxTimer, t;
        SpriteRenderer glow, warnRing, core, fireFill;

        public static EmberVent Create(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Ember Vent");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var v = go.AddComponent<EmberVent>();
            v.Build();
            return v;
        }

        void Build()
        {
            var lmT = LevelManager.I;
            bool marsh = lmT != null && lmT.Level != null && lmT.Level.Theme == LevelTheme.Marsh;
            DisplayName = marsh ? "Marsh Gas" : "Ember Vent";
            gameObject.name = DisplayName;
            coreIdle = marsh ? Palette.Hex("#3E7A3A") : Palette.Hex("#C24A12");
            coreHot = marsh ? Palette.Hex("#C8FF6A") : Palette.Gold;
            flameA = marsh ? new Color(0.45f, 1f, 0.3f, 0.9f) : new Color(1f, 0.55f, 0.1f, 0.9f);
            flameB = marsh ? new Color(0.8f, 1f, 0.5f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.9f);
            smoke = marsh ? new Color(0.35f, 0.45f, 0.3f, 0.5f) : new Color(0.35f, 0.3f, 0.28f, 0.5f);
            Art.Make("scorch", transform, Art.Circle, marsh ? Palette.Hex("#1E2A22") : Palette.Hex("#2A1F1A"), Vector2.zero, Vector2.one * Radius * 2.2f, 4);
            Art.Make("scorchInner", transform, Art.Circle, marsh ? Palette.Hex("#142018") : Palette.Hex("#1C1512"), Vector2.zero, Vector2.one * Radius * 1.4f, 4);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f;
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Art.Make("crack", transform, Art.Square, marsh ? Palette.Hex("#2E5A2A") : Palette.Hex("#7A2E10"), dir * Radius * 0.45f, new Vector2(Radius * 0.9f, 0.08f), 5, a);
            }
            core = Art.Make("core", transform, Art.Circle, coreIdle, Vector2.zero, Vector2.one * 0.5f, 5);
            fireFill = Art.Make("fire", transform, Art.Circle, new Color(flameA.r, flameA.g, flameA.b, 0f), Vector2.zero, Vector2.one * Radius * 2f, 6);
            glow = Art.Make("glow", transform, Art.Glow, new Color(flameA.r, flameA.g * 0.9f, flameA.b, 0.2f), Vector2.zero, Vector2.one * Radius * 3f, 6);
            warnRing = Art.Make("warn", transform, Art.Ring, new Color(flameA.r, flameA.g, flameA.b, 0f), Vector2.zero, Vector2.one * Radius * 2f, 30);
            Current = Phase.Dormant;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            timer -= dt;
            var lm = LevelManager.I;

            switch (Current)
            {
                case Phase.Dormant:
                    SetAlpha(glow, 0.15f + 0.06f * Mathf.Sin(t * 2f));
                    SetAlpha(warnRing, 0f);
                    SetAlpha(fireFill, 0f);
                    core.color = coreIdle;
                    if (timer <= 0f)
                    {
                        Current = Phase.Warning;
                        timer = WarningTime;
                        if (lm != null) lm.OnVentWarning(this);
                        AudioManager.Play(Sfx.Warn, 0.5f);
                    }
                    break;

                case Phase.Warning:
                {
                    float k = 1f - timer / WarningTime;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * (10f + 10f * k));
                    SetAlpha(warnRing, 0.35f + 0.5f * pulse);
                    SetAlpha(glow, 0.25f + 0.45f * k);
                    SetAlpha(fireFill, 0.1f * k);
                    core.color = Color.Lerp(coreIdle, coreHot, pulse);
                    Smoke(dt, 0.12f);
                    if (timer <= 0f)
                    {
                        Current = Phase.Erupting;
                        timer = EruptTime;
                        if (lm != null && lm.CamCtl != null) lm.CamCtl.Shake(0.3f, 0.2f);
                        AudioManager.Play(Sfx.Eruption, 0.8f);
                        Fx.Ring(Position, flameB, 0.3f, Radius * 1.3f, 0.4f);
                    }
                    break;
                }

                case Phase.Erupting:
                    SetAlpha(warnRing, 0.85f);
                    SetAlpha(glow, 0.8f + 0.15f * Mathf.Sin(t * 25f));
                    SetAlpha(fireFill, 0.35f + 0.1f * Mathf.Sin(t * 18f));
                    core.color = coreHot;
                    Flames(dt);
                    if (lm != null) Burn(lm, dt);
                    if (timer <= 0f)
                    {
                        Current = Phase.Dormant;
                        timer = DormantTime;
                    }
                    break;
            }
        }

        void Burn(LevelManager lm, float dt)
        {
            foreach (var e in lm.EnemiesInRadius(Position, Radius + 0.2f))
            {
                e.Reveal(0.5f);
                e.TakeDamage(Dps * dt, DamageKind.True);
            }
            var hero = lm.HeroUnit;
            if (hero != null && !hero.Dead && Vector2.Distance(hero.Position, Position) <= Radius + 0.2f)
                hero.TakeDamage(Dps * dt);
        }

        void Flames(float dt)
        {
            fxTimer -= dt;
            while (fxTimer <= 0f)
            {
                fxTimer += 0.03f;
                Vector2 p = Position + Random.insideUnitCircle * Radius * 0.9f;
                Color c = Random.value < 0.5f ? flameA : flameB;
                Fx.Burst(p, c, Random.Range(0.4f, 0.8f), 0.1f, Random.Range(0.4f, 0.7f), null, 47, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.5f, 3f), 0f));
            }
        }

        void Smoke(float dt, float every)
        {
            fxTimer -= dt;
            if (fxTimer > 0f) return;
            fxTimer = every;
            Vector2 p = Position + Random.insideUnitCircle * Radius * 0.6f;
            Fx.Burst(p, smoke, 0.4f, 1.2f, 1f, Art.Circle, 46, new Vector3(0f, 0.8f, 0f));
        }

        static void SetAlpha(SpriteRenderer sr, float a)
        {
            Color c = sr.color;
            c.a = Mathf.Clamp01(a);
            sr.color = c;
        }
    }
}

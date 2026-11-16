using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Gorruk Skullsplitter, Orc Warlord (wave 30). Two unique mechanics, both telegraphed and
    /// both INTERRUPTIBLE by any stun during the wind-up:
    ///  1. Axe Throw: every 9s he marks the tower with the most gold invested (red line, 1.2s),
    ///     then hurls his axe and knocks that tower out for 5 seconds.
    ///  2. War Cry: at half health he plants his feet and draws breath for 2s (growing red ring),
    ///     then roars and every orc on the field moves 50% faster. Interrupted, he retries in 8s.
    /// </summary>
    public class BossGorruk : Enemy
    {
        public const float AxeInterval = 9f;
        public const float AxeTelegraph = 1.2f;
        public const float AxeRange = 9f;
        public const float AxeDisable = 5f;
        public const float WarCryWindUp = 2f;

        public bool Enraged { get; private set; }

        float axeTimer = 5f;
        float telegraphTimer;
        float warCryWind, warCryRetry, roarTimer;
        bool warnedWarCry;
        Tower axeTarget;
        SpriteRenderer telegraphLine, telegraphMark, warCryRing;

        public override bool WindingUp => axeTarget != null || warCryWind > 0f;
        public override string WindUpName => warCryWind > 0f ? "WAR CRY" : axeTarget != null ? "AXE THROW" : "";
        public override float WindUpProgress => warCryWind > 0f ? 1f - warCryWind / WarCryWindUp : axeTarget != null ? 1f - telegraphTimer / AxeTelegraph : 0f;
        protected override bool CanMove => warCryWind <= 0f && roarTimer <= 0f;

        protected override void Setup(EnemyType type, PathRoute route, int wave)
        {
            base.Setup(type, route, wave);
            telegraphLine = Art.Make("axeTelegraph", null, Art.Square, new Color(1f, 0.2f, 0.15f, 0.6f), Vector2.zero, Vector2.one, 30);
            telegraphMark = Art.Make("axeTarget", null, Art.Ring, new Color(1f, 0.2f, 0.15f, 0.9f), Vector2.zero, Vector2.one * 1.6f, 31);
            warCryRing = Art.Make("warCryRing", transform, Art.Ring, new Color(1f, 0.25f, 0.15f, 0.9f), Vector2.zero, Vector2.one, 32);
            telegraphLine.enabled = telegraphMark.enabled = warCryRing.enabled = false;
        }

        protected override void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (roarTimer > 0f)
            {
                roarTimer -= dt;
                if (body != null) body.localScale = Vector3.one * (1.1f + 0.08f * Mathf.Sin(Time.time * 30f));
            }
            base.Update();
            if (!Alive || Stunned) return;

            UpdateWarCry(dt);
            UpdateAxe(dt);
        }

        void UpdateWarCry(float dt)
        {
            if (warCryRetry > 0f) warCryRetry -= dt;
            if (!Enraged && warCryWind <= 0f && warCryRetry <= 0f && Hp <= MaxHp * 0.5f)
            {
                warCryWind = WarCryWindUp;
                warCryRing.enabled = true;
                AudioManager.Play(Sfx.Warn);
                if (!warnedWarCry && LevelManager.I != null)
                {
                    warnedWarCry = true;
                    LevelManager.I.Announce("Gorruk is drawing breath!", "STUN HIM NOW (Shield Slam, Earthshaker or the Rockslide) to interrupt the War Cry.", "Captain Brand", Palette.Interrupt, 5f);
                }
            }
            if (warCryWind > 0f)
            {
                warCryWind -= dt;
                float k = 1f - warCryWind / WarCryWindUp;
                warCryRing.transform.localScale = Vector3.one * Mathf.Lerp(6f, 1.8f, k);
                warCryRing.color = new Color(1f, 0.25f, 0.15f, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * (6f + 14f * k))));
                if (body != null) body.localScale = Vector3.one * (1f + 0.12f * k);
                if (warCryWind <= 0f)
                {
                    warCryRing.enabled = false;
                    WarCry();
                }
            }
        }

        void UpdateAxe(float dt)
        {
            if (axeTarget == null && (telegraphLine.enabled || telegraphMark.enabled))
            {
                // Target was sold mid-telegraph.
                telegraphLine.enabled = telegraphMark.enabled = false;
                axeTimer = 2f;
            }

            if (axeTarget != null)
            {
                telegraphTimer -= dt;
                Vector2 a = Position, b = axeTarget.Position;
                Vector2 d = b - a;
                telegraphLine.transform.position = (a + b) * 0.5f;
                telegraphLine.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                telegraphLine.transform.localScale = new Vector3(d.magnitude, 0.08f + 0.06f * Mathf.Abs(Mathf.Sin(Time.time * 20f)), 1f);
                telegraphMark.transform.position = b;
                telegraphMark.transform.localScale = Vector3.one * (1.4f + 0.3f * telegraphTimer);
                if (telegraphTimer <= 0f)
                {
                    ThrowAxe(axeTarget);
                    axeTarget = null;
                    telegraphLine.enabled = telegraphMark.enabled = false;
                    axeTimer = AxeInterval;
                }
                return;
            }

            if (warCryWind > 0f) return;
            axeTimer -= dt;
            if (axeTimer > 0f) return;
            Tower t = PickTarget();
            if (t == null) { axeTimer = 1f; return; }
            axeTarget = t;
            telegraphTimer = AxeTelegraph;
            telegraphLine.enabled = telegraphMark.enabled = true;
            AudioManager.Play(Sfx.Warn, 0.7f);
        }

        protected override void OnInterrupted()
        {
            if (warCryWind > 0f)
            {
                warCryWind = 0f;
                warCryRetry = 8f;
                warCryRing.enabled = false;
                if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.3f, "WAR CRY INTERRUPTED!", Palette.Interrupt);
            }
            if (axeTarget != null)
            {
                axeTarget = null;
                telegraphLine.enabled = telegraphMark.enabled = false;
                axeTimer = AxeInterval;
                if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.3f, "AXE THROW INTERRUPTED!", Palette.Interrupt);
            }
        }

        Tower PickTarget()
        {
            var lm = LevelManager.I;
            if (lm == null) return null;
            Tower best = null;
            int bestInvest = -1;
            float bestDist = float.MaxValue;
            foreach (var t in lm.Towers)
            {
                if (t == null || t.Disabled) continue;
                float dist = Vector2.Distance(t.Position, Position);
                if (dist > AxeRange) continue;
                if (t.Invested > bestInvest || (t.Invested == bestInvest && dist < bestDist))
                {
                    best = t; bestInvest = t.Invested; bestDist = dist;
                }
            }
            return best;
        }

        void ThrowAxe(Tower target)
        {
            Tower victim = target;
            AudioManager.Play(Sfx.Axe);
            Projectile.Lob(Position, victim.Position, 0.55f, 1.6f, Art.Triangle, Palette.Hex("#C8C2BA"), new Vector2(0.55f, 0.7f), 900f, landPos =>
            {
                Fx.Burst(landPos, new Color(1f, 0.6f, 0.3f, 0.9f), 0.6f, 2.2f, 0.35f);
                AudioManager.Play(Sfx.Impact, 0.8f);
                if (victim != null)
                {
                    victim.Disable(AxeDisable);
                    if (LevelManager.I != null) LevelManager.I.AddPopup(victim.Position + Vector2.up * 0.9f, "Knocked out!", Palette.Bad);
                }
            });
        }

        void WarCry()
        {
            Enraged = true;
            roarTimer = 1.0f;
            AudioManager.Play(Sfx.Roar);
            var lm = LevelManager.I;
            if (lm == null) return;
            foreach (var e in lm.Enemies)
                if (e != null && e.Alive) e.ApplyWarCry();
            for (int i = 0; i < 3; i++) Fx.Ring(Position, new Color(1f, 0.3f, 0.15f, 0.8f - i * 0.2f), 0.6f, 6f + i * 3f, 0.6f + i * 0.25f);
            if (lm.CamCtl != null) lm.CamCtl.Shake(0.5f, 0.35f);
            lm.Announce("WAR CRY!", "Every orc on the field is 50% faster. Mage Towers with Dispel strip the frenzy.", "Captain Brand", Palette.Bad, 5f);
        }

        protected override void Die()
        {
            CleanupTelegraph();
            for (int i = 0; i < 4; i++) Fx.Ring(Position, new Color(1f, 0.75f, 0.3f, 0.9f), 0.5f, 3f + i * 1.5f, 0.5f + i * 0.2f);
            base.Die();
        }

        void CleanupTelegraph()
        {
            if (telegraphLine != null) Destroy(telegraphLine.gameObject);
            if (telegraphMark != null) Destroy(telegraphMark.gameObject);
        }

        void OnDestroy()
        {
            CleanupTelegraph();
        }
    }
}

using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Gorruk Skullsplitter, Orc Warlord (Level 1 boss). Two unique mechanics:
    ///  1. Axe Throw: every 9s he marks a tower (red telegraph line for 1s), then hurls his axe
    ///     and knocks that tower out for 5 seconds. He targets the tower with the most gold invested.
    ///  2. War Cry: at half health he stops to roar, and every orc on the field moves 50% faster.
    ///     Mage Towers with Dispel can strip the frenzy from individual orcs.
    /// </summary>
    public class BossGorruk : Enemy
    {
        public const float AxeInterval = 9f;
        public const float AxeTelegraph = 1.0f;
        public const float AxeRange = 9f;
        public const float AxeDisable = 5f;

        public bool Enraged { get; private set; }
        public bool Telegraphing => axeTarget != null;

        float axeTimer = 5f;
        float telegraphTimer;
        float roarTimer;
        Tower axeTarget;
        SpriteRenderer telegraphLine, telegraphMark;

        protected override void Setup(EnemyType type, PathRoute route)
        {
            base.Setup(type, route);
            bar.AlwaysShow = true;
            bar.Set(1f);
            telegraphLine = Art.Make("axeTelegraph", null, Art.Square, new Color(1f, 0.2f, 0.15f, 0.6f), Vector2.zero, Vector2.one, 30);
            telegraphMark = Art.Make("axeTarget", null, Art.Ring, new Color(1f, 0.2f, 0.15f, 0.9f), Vector2.zero, Vector2.one * 1.6f, 31);
            telegraphLine.enabled = false;
            telegraphMark.enabled = false;
        }

        protected override void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!Enraged && Hp <= MaxHp * 0.5f) WarCry();

            if (roarTimer > 0f)
            {
                roarTimer -= dt;
                if (body != null) body.localScale = Vector3.one * (1.1f + 0.08f * Mathf.Sin(Time.time * 30f));
            }
            else
            {
                base.Update();
                if (!Alive) return;
            }

            UpdateAxe(dt);
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

            axeTimer -= dt;
            if (axeTimer > 0f) return;
            Tower t = PickTarget();
            if (t == null) { axeTimer = 1f; return; }
            axeTarget = t;
            telegraphTimer = AxeTelegraph;
            telegraphLine.enabled = telegraphMark.enabled = true;
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
            Projectile.Lob(Position, victim.Position, 0.55f, 1.6f, Art.Triangle, Palette.Hex("#C8C2BA"), new Vector2(0.55f, 0.7f), 900f, landPos =>
            {
                Fx.Burst(landPos, new Color(1f, 0.6f, 0.3f, 0.9f), 0.6f, 2.2f, 0.35f);
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
            roarTimer = 1.3f;
            var lm = LevelManager.I;
            if (lm == null) return;
            foreach (var e in lm.Enemies)
                if (e != null && e.Alive) e.ApplyWarCry();
            for (int i = 0; i < 3; i++) Fx.Ring(Position, new Color(1f, 0.3f, 0.15f, 0.8f - i * 0.2f), 0.6f, 6f + i * 3f, 0.6f + i * 0.25f);
            if (lm.CamCtl != null) lm.CamCtl.Shake(0.5f, 0.35f);
            lm.Announce("WAR CRY!", "Gorruk's roar drives every orc on the field 50% faster. Mage Towers with Dispel can strip the frenzy.", "Captain Brand", Palette.Bad, 6f);
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

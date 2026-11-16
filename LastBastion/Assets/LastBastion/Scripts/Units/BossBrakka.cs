using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Warchief Brakka (wave 10, and the tutorial's final wave). Unique mechanic: Rally Horn.
    /// Every 10s he stops and winds up for 2s (green ring closing in); if not interrupted, every orc
    /// within 5 units heals 35% of its health.
    /// </summary>
    public class BossBrakka : Enemy
    {
        public const float RallyInterval = 10f;
        public const float RallyWindUp = 2f;
        public const float RallyRadius = 5f;

        float rallyTimer = 6f, wind;
        SpriteRenderer ring;

        public override bool WindingUp => wind > 0f;
        public override string WindUpName => wind > 0f ? "RALLY HORN" : "";
        public override float WindUpProgress => wind > 0f ? 1f - wind / RallyWindUp : 0f;
        protected override bool CanMove => wind <= 0f;

        protected override void Setup(EnemyType type, PathRoute route, int wave)
        {
            base.Setup(type, route, wave);
            ring = Art.Make("rallyRing", transform, Art.Ring, new Color(0.5f, 1f, 0.4f, 0.9f), Vector2.zero, Vector2.one, 32);
            ring.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive || Stunned) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (wind > 0f)
            {
                wind -= dt;
                float k = 1f - wind / RallyWindUp;
                ring.transform.localScale = Vector3.one * Mathf.Lerp(RallyRadius * 2f, 1.6f, k);
                ring.color = new Color(0.5f, 1f, 0.4f, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * (6f + 12f * k))));
                if (wind <= 0f) Rally();
                return;
            }

            rallyTimer -= dt;
            if (rallyTimer <= 0f)
            {
                wind = RallyWindUp;
                ring.enabled = true;
                AudioManager.Play(Sfx.Warn);
            }
        }

        void Rally()
        {
            ring.enabled = false;
            rallyTimer = RallyInterval;
            AudioManager.Play(Sfx.Horn);
            Fx.Ring(Position, new Color(0.5f, 1f, 0.4f, 0.8f), 0.5f, RallyRadius, 0.6f);
            var lm = LevelManager.I;
            if (lm == null) return;
            foreach (var e in lm.EnemiesInRadius(Position, RallyRadius))
            {
                e.Heal(e.MaxHp * 0.35f);
                Fx.Burst(e.Position, new Color(0.5f, 1f, 0.4f, 0.8f), 0.4f, 1.2f, 0.5f);
            }
            lm.AddPopup(Position + Vector2.up * 1.2f, "Rallied! Orcs healed", Palette.Good);
        }

        protected override void OnInterrupted()
        {
            wind = 0f;
            ring.enabled = false;
            rallyTimer = RallyInterval;
            if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.2f, "RALLY INTERRUPTED!", Palette.Interrupt);
        }
    }
}

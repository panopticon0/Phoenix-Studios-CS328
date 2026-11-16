using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Death Knight Varn (Weeping Marsh, wave 10). Unique mechanic: Death Charge.
    /// Every 9s he lowers his lance and winds up for 1.5s (red arrow ahead of him), then charges at
    /// 4x speed for 1.4s, trampling straight through Sir Aldric's block.
    /// Interruptible: stun him during the wind-up.
    /// </summary>
    public class BossDeathKnight : Enemy
    {
        const float Interval = 9f;
        const float WindUp = 1.5f;
        const float ChargeTime = 1.4f;

        float timer = 5f, wind, charge, trail;
        SpriteRenderer ring, arrow;

        public override bool WindingUp => wind > 0f;
        public override string WindUpName => wind > 0f ? "DEATH CHARGE" : "";
        public override float WindUpProgress => wind > 0f ? 1f - wind / WindUp : 0f;
        protected override bool CanMove => wind <= 0f;
        protected override float SpeedMultiplier => charge > 0f ? 4f : 1f;
        protected override bool IgnoresBlock => charge > 0f;

        protected override void Setup(EnemyType type, PathRoute route, int wave)
        {
            base.Setup(type, route, wave);
            ring = Art.Make("chargeRing", transform, Art.Ring, new Color(1f, 0.25f, 0.2f, 0.9f), Vector2.zero, Vector2.one * 2f, 32);
            arrow = Art.Make("chargeArrow", null, Art.Triangle, new Color(1f, 0.25f, 0.2f, 0.6f), Vector2.zero, new Vector2(1f, 1.4f), 31);
            ring.enabled = arrow.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (charge > 0f)
            {
                charge -= dt;
                trail -= dt;
                if (trail <= 0f)
                {
                    trail = 0.05f;
                    Fx.Burst(Position, new Color(0.4f, 0.15f, 0.2f, 0.6f), 1.2f, 0.3f, 0.4f, Art.Circle, 16);
                }
                return;
            }
            if (Stunned) return;

            if (wind > 0f)
            {
                wind -= dt;
                float k = 1f - wind / WindUp;
                ring.transform.localScale = Vector3.one * (2.2f - 0.6f * k);
                ring.color = new Color(1f, 0.25f, 0.2f, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 16f)));
                Vector2 dir = path.DirectionAt(Distance);
                Vector2 ahead = Position + dir * (2.2f + 0.6f * Mathf.Sin(Time.time * 10f));
                arrow.transform.position = new Vector3(ahead.x, ahead.y, 0f);
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                if (wind <= 0f)
                {
                    ring.enabled = arrow.enabled = false;
                    charge = ChargeTime;
                    timer = Interval;
                    AudioManager.Play(Sfx.Roar, 0.7f);
                    var lm = LevelManager.I;
                    if (lm != null && lm.CamCtl != null) lm.CamCtl.Shake(0.4f, 0.2f);
                }
                return;
            }

            timer -= dt;
            if (timer <= 0f)
            {
                wind = WindUp;
                ring.enabled = arrow.enabled = true;
                AudioManager.Play(Sfx.Warn);
            }
        }

        protected override void OnInterrupted()
        {
            wind = 0f;
            timer = Interval;
            ring.enabled = arrow.enabled = false;
            if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.2f, "CHARGE INTERRUPTED!", Palette.Interrupt);
        }

        void OnDestroy()
        {
            if (arrow != null) Destroy(arrow.gameObject);
        }
    }
}

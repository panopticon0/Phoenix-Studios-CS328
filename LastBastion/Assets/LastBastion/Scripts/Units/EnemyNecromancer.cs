using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Necromancer (Weeping Marsh). Unique mechanic: Raise Dead. Every 7s he stops and performs a
    /// 1.2s ritual (green ring closing in), then raises two Skeletons from the mud behind him.
    /// Interruptible: stun him during the ritual.
    /// </summary>
    public class EnemyNecromancer : Enemy
    {
        const float Interval = 7f;
        const float WindUp = 1.2f;
        const int RaiseCount = 2;

        float timer = 3f, wind;
        SpriteRenderer ring;

        public override bool WindingUp => wind > 0f;
        public override string WindUpName => wind > 0f ? "RAISE DEAD" : "";
        public override float WindUpProgress => wind > 0f ? 1f - wind / WindUp : 0f;
        protected override bool CanMove => wind <= 0f;

        protected override void Setup(EnemyType type, PathRoute route, int wave)
        {
            base.Setup(type, route, wave);
            ring = Art.Make("ritualRing", transform, Art.Ring, new Color(0.55f, 1f, 0.45f, 0.9f), Vector2.zero, Vector2.one, 32);
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
                float k = 1f - wind / WindUp;
                ring.transform.localScale = Vector3.one * Mathf.Lerp(3f, 1f, k);
                ring.color = new Color(0.55f, 1f, 0.45f, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 14f)));
                if (wind <= 0f) Raise();
                return;
            }
            timer -= dt;
            if (timer <= 0f)
            {
                wind = WindUp;
                ring.enabled = true;
            }
        }

        void Raise()
        {
            ring.enabled = false;
            timer = Interval;
            AudioManager.Play(Sfx.Raise, 0.8f);
            var lm = LevelManager.I;
            if (lm == null) return;
            for (int i = 0; i < RaiseCount; i++)
            {
                var e = lm.SpawnAt(EnemyType.Skeleton, Distance - 0.8f - i * 0.6f, SpawnWave, true);
                if (e != null) Fx.Burst(e.Position, new Color(0.55f, 1f, 0.45f, 0.8f), 0.4f, 1.4f, 0.5f);
            }
        }

        protected override void OnInterrupted()
        {
            wind = 0f;
            ring.enabled = false;
            timer = Interval;
            if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 0.9f, "RITUAL INTERRUPTED!", Palette.Interrupt);
        }
    }
}

using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Banshee (Weeping Marsh). Unique mechanic: Wail. Every 8s she stops, locks onto the nearest
    /// tower (pale line) and winds up for 1.4s, then freezes that tower for 3 seconds.
    /// Interruptible: stun her during the wind-up.
    /// </summary>
    public class EnemyBanshee : Enemy
    {
        const float Interval = 8f;
        const float WindUp = 1.4f;
        const float Range = 6.5f;
        const float FreezeTime = 3f;

        float timer = 4f, wind;
        Tower target;
        SpriteRenderer line, ring;

        public override bool WindingUp => wind > 0f;
        public override string WindUpName => wind > 0f ? "WAIL" : "";
        public override float WindUpProgress => wind > 0f ? 1f - wind / WindUp : 0f;
        protected override bool CanMove => wind <= 0f;

        protected override void Setup(EnemyType type, PathRoute route, int wave)
        {
            base.Setup(type, route, wave);
            line = Art.Make("wailLine", null, Art.Square, new Color(0.75f, 0.9f, 1f, 0.55f), Vector2.zero, Vector2.one, 30);
            ring = Art.Make("wailRing", transform, Art.Ring, new Color(0.75f, 0.9f, 1f, 0.9f), Vector2.zero, Vector2.one, 32);
            line.enabled = ring.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive || Stunned) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (wind > 0f)
            {
                if (target == null) { Cancel(2f); return; }
                wind -= dt;
                float k = 1f - wind / WindUp;
                Vector2 a = Position, b = target.Position, d = b - a;
                line.transform.position = (a + b) * 0.5f;
                line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                line.transform.localScale = new Vector3(d.magnitude, 0.06f + 0.08f * k, 1f);
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.4f, k);
                ring.color = new Color(0.75f, 0.9f, 1f, 0.9f * (1f - k) + 0.1f);
                if (wind <= 0f) Wail();
                return;
            }

            timer -= dt;
            if (timer > 0f) return;
            target = Nearest();
            if (target == null) { timer = 1f; return; }
            wind = WindUp;
            line.enabled = ring.enabled = true;
            AudioManager.Play(Sfx.Warn, 0.5f);
        }

        Tower Nearest()
        {
            var lm = LevelManager.I;
            if (lm == null) return null;
            Tower best = null;
            float bestD = Range;
            foreach (var t in lm.Towers)
            {
                if (t == null || t.Disabled) continue;
                float d = Vector2.Distance(t.Position, Position);
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        void Wail()
        {
            line.enabled = ring.enabled = false;
            timer = Interval;
            AudioManager.Play(Sfx.Wail);
            Fx.Ring(Position, new Color(0.75f, 0.9f, 1f, 0.8f), 0.4f, 3f, 0.5f);
            if (target != null)
            {
                target.Disable(FreezeTime, true);
                if (LevelManager.I != null) LevelManager.I.AddPopup(target.Position + Vector2.up * 0.9f, "Frozen by a wail!", Palette.Frost);
            }
            target = null;
        }

        void Cancel(float retry)
        {
            wind = 0f;
            target = null;
            timer = retry;
            if (line != null) line.enabled = false;
            if (ring != null) ring.enabled = false;
        }

        protected override void OnInterrupted()
        {
            Cancel(Interval);
            if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 0.9f, "WAIL INTERRUPTED!", Palette.Interrupt);
        }

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
        }
    }
}

using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Power-up: Dragonfire. Orcs drop Ember Shards; each shard is one charge (max 3). Drag the
    /// Dragonfire card from the shop onto the map: a dragon's shadow sweeps over and breathes fire,
    /// dealing 180 magic damage, burning the ground for 4 seconds and revealing invisible enemies.
    /// </summary>
    public class DragonfireStrike : MonoBehaviour
    {
        const float Delay = 0.55f;
        const float FieldTime = 4f;
        Vector2 at;
        float age, tick;
        bool struck;
        SpriteRenderer target, shadow, field;

        public static void Cast(Vector2 at)
        {
            var go = new GameObject("Dragonfire");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var d = go.AddComponent<DragonfireStrike>();
            d.at = at;
            float r = Balance.DragonfireRadius;
            d.target = Art.Make("target", go.transform, Art.Ring, new Color(1f, 0.45f, 0.15f, 0.9f), Vector2.zero, Vector2.one * r * 2f, 33);
            d.field = Art.Make("field", go.transform, Art.Glow, new Color(1f, 0.45f, 0.1f, 0f), Vector2.zero, Vector2.one * r * 2.6f, 8);
            d.shadow = Art.Make("dragonShadow", null, Art.Triangle, new Color(0f, 0f, 0f, 0.45f), at + new Vector2(-14f, 4f), new Vector2(3.6f, 2.4f), 49, -90f);
            AudioManager.Play(Sfx.Roar, 0.6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            float r = Balance.DragonfireRadius;

            if (!struck)
            {
                float k = age / Delay;
                if (shadow != null) shadow.transform.position = Vector3.Lerp(new Vector3(at.x - 14f, at.y + 4f, 0f), new Vector3(at.x + 14f, at.y - 2f, 0f), k);
                target.transform.localScale = Vector3.one * r * 2f * (1f + 0.1f * Mathf.Sin(age * 30f));
                if (k >= 1f) Strike();
                return;
            }

            if (shadow != null)
            {
                shadow.transform.position += new Vector3(28f * dt, -6f * dt, 0f);
                if (shadow.transform.position.x > at.x + 30f) { Destroy(shadow.gameObject); shadow = null; }
            }

            float f = age - Delay;
            field.color = new Color(1f, 0.45f, 0.1f, 0.55f * (1f - f / FieldTime) + 0.1f * Mathf.Sin(age * 20f));
            tick -= dt;
            if (tick <= 0f)
            {
                tick = 0.3f;
                for (int i = 0; i < 4; i++)
                    Fx.Burst(at + Random.insideUnitCircle * r, new Color(1f, Random.Range(0.4f, 0.8f), 0.1f, 0.85f), Random.Range(0.4f, 0.8f), 0.1f, 0.6f, null, 46, new Vector3(0f, Random.Range(1f, 2.2f), 0f));
                var lm = LevelManager.I;
                if (lm != null)
                    foreach (var e in lm.EnemiesInRadius(at, r))
                    {
                        e.Reveal(1f);
                        e.ApplyBurn(Balance.DragonfireBurnDps, 1f);
                    }
            }
            if (f >= FieldTime) Destroy(gameObject);
        }

        void Strike()
        {
            struck = true;
            target.enabled = false;
            float r = Balance.DragonfireRadius;
            AudioManager.Play(Sfx.Dragonfire);
            Fx.Burst(at, new Color(1f, 0.6f, 0.2f, 0.95f), 1f, r * 3f, 0.5f);
            Fx.Ring(at, new Color(1f, 0.85f, 0.4f, 0.9f), 0.4f, r * 1.2f, 0.45f);
            Fx.Sparks(at, new Color(1f, 0.6f, 0.2f, 1f), 30, 7f, 0.7f);
            var lm = LevelManager.I;
            if (lm == null) return;
            if (lm.CamCtl != null) lm.CamCtl.Shake(0.4f, 0.25f);
            foreach (var e in lm.EnemiesInRadius(at, r))
            {
                e.Reveal(Balance.DragonfireRevealTime);
                e.TakeDamage(Balance.DragonfireDamage, DamageKind.Magic);
                if (e.Alive) e.ApplyBurn(Balance.DragonfireBurnDps, Balance.DragonfireBurnTime);
            }
        }

        void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }
    }
}

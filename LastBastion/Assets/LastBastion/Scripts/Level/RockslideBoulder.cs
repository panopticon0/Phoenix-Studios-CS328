using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>One boulder rolling down the Rockslide lane. Hits each orc (and the hero) once.</summary>
    public class RockslideBoulder : MonoBehaviour
    {
        const float Speed = 9f;
        const float HitRadius = 0.95f;
        Vector2 from, to;
        float travelled, length;
        readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        bool hitHero, log;
        Transform visual;

        public static void Launch(Vector2 from, Vector2 to, bool log = false)
        {
            var go = new GameObject("Boulder");
            go.transform.position = new Vector3(from.x, from.y, 0f);
            var b = go.AddComponent<RockslideBoulder>();
            b.from = from;
            b.to = to;
            b.length = Vector2.Distance(from, to);
            b.log = log;
            Art.Make("shadow", go.transform, Art.Circle, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.1f, -0.15f), new Vector2(1.2f, 0.8f), 34);
            b.visual = new GameObject("rock").transform;
            b.visual.SetParent(go.transform, false);
            if (log)
            {
                // A rolling log, seen end-on: bark, wood, growth rings.
                Art.Make("bark", b.visual, Art.Circle, Palette.Hex("#3E3022"), Vector2.zero, Vector2.one * 1.15f, 35);
                Art.Make("wood", b.visual, Art.Circle, Palette.Hex("#A88A5E"), Vector2.zero, Vector2.one * 0.9f, 36);
                Art.Make("ring1", b.visual, Art.Ring, Palette.Hex("#7A6040"), Vector2.zero, Vector2.one * 0.6f, 37);
                Art.Make("ring2", b.visual, Art.Ring, Palette.Hex("#7A6040"), Vector2.zero, Vector2.one * 0.32f, 37);
                Art.Make("crack", b.visual, Art.Square, Palette.Hex("#5A4630"), new Vector2(0.1f, 0f), new Vector2(0.4f, 0.05f), 38, 20f);
            }
            else
            {
                Art.Make("rock", b.visual, Art.Circle, Palette.Hex("#7A6E64"), Vector2.zero, Vector2.one * 1.15f, 35);
                Art.Make("rockLight", b.visual, Art.Circle, Palette.Hex("#958A80"), new Vector2(-0.18f, 0.18f), Vector2.one * 0.5f, 36);
                Art.Make("rockCrack", b.visual, Art.Square, Palette.Hex("#3A302A"), new Vector2(0.1f, -0.1f), new Vector2(0.5f, 0.06f), 36, 30f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            travelled += Speed * dt;
            float k = Mathf.Clamp01(travelled / length);
            Vector2 p = Vector2.Lerp(from, to, k);
            transform.position = new Vector3(p.x, p.y, 0f);
            visual.Rotate(0f, 0f, -540f * dt);
            if (Random.value < 0.5f) Fx.Burst(p + Random.insideUnitCircle * 0.4f, new Color(0.5f, 0.42f, 0.35f, 0.5f), 0.5f, 1.2f, 0.6f, Art.Circle, 13);

            var lm = LevelManager.I;
            if (lm != null)
            {
                foreach (var e in lm.EnemiesInRadius(p, HitRadius + 0.3f))
                {
                    if (hit.Contains(e)) continue;
                    hit.Add(e);
                    e.Reveal(1f);
                    e.Stun(1f);
                    e.TakeDamage(Balance.RockslideDamage, DamageKind.Physical);
                    Fx.Burst(e.Position, new Color(1f, 0.85f, 0.6f, 0.8f), 0.5f, 1.6f, 0.3f);
                    AudioManager.Play(Sfx.Impact, 0.6f);
                }
                var hero = lm.HeroUnit;
                if (!hitHero && hero != null && !hero.Dead && Vector2.Distance(hero.Position, p) <= HitRadius + 0.4f)
                {
                    hitHero = true;
                    hero.TakeDamage(90f);
                    lm.AddPopup(hero.Position + Vector2.up, log ? "Flattened by a log!" : "Crushed by the Rockslide!", Palette.Bad);
                }
            }

            if (k >= 1f)
            {
                Fx.Burst(p, new Color(0.5f, 0.42f, 0.35f, 0.7f), 1f, 2.5f, 0.6f, Art.Circle, 13);
                Destroy(gameObject);
            }
        }
    }
}

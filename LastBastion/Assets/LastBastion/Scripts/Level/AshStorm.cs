using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Hazard: Ash Storm. A cloud of volcanic ash drifts across the map every ~45 seconds.
    /// Unique mechanic: it BLINDS TOWERS. Any tower inside the cloud loses 40% of its range while the
    /// cloud passes. Spread towers out, or keep some out of its path. Sir Aldric is unaffected.
    /// </summary>
    public class AshStorm : MonoBehaviour
    {
        public const float Radius = 4.6f;
        const float Speed = 1.7f;
        const float FirstDelay = 70f;
        const float Interval = 45f;

        public bool Active { get; private set; }
        public Vector2 Center => transform.position;
        public float NextIn => Active ? 0f : timer;
        /// <summary>"Ash Storm" on the Ashen Pass, "Marsh Fog" in the Weeping Marsh.</summary>
        public string DisplayName { get; private set; }
        Color puffColor = new Color(0.55f, 0.52f, 0.5f, 0.55f), moteColor = new Color(0.7f, 0.66f, 0.62f, 0.6f);

        float timer = FirstDelay, dir = 1f, t, fx;
        readonly List<SpriteRenderer> puffs = new List<SpriteRenderer>();
        readonly List<Vector2> puffOffsets = new List<Vector2>();
        SpriteRenderer edge;
        Transform cloud;

        public static AshStorm Create(Transform parent)
        {
            var go = new GameObject("Ash Storm");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AshStorm>();
            s.Build();
            return s;
        }

        void Build()
        {
            var lmT = LevelManager.I;
            bool marsh = lmT != null && lmT.Level != null && lmT.Level.Theme == LevelTheme.Marsh;
            DisplayName = marsh ? "Marsh Fog" : "Ash Storm";
            gameObject.name = DisplayName;
            if (marsh)
            {
                puffColor = new Color(0.55f, 0.66f, 0.6f, 0.55f);
                moteColor = new Color(0.7f, 0.85f, 0.75f, 0.45f);
            }
            cloud = new GameObject("Cloud").transform;
            cloud.SetParent(transform, false);
            for (int i = 0; i < 9; i++)
            {
                Vector2 o = i == 0 ? Vector2.zero : Random.insideUnitCircle * Radius * 0.6f;
                float s = Random.Range(3.2f, 5.2f);
                var sr = Art.Make("ash", cloud, Art.Glow, puffColor, o, Vector2.one * s, 48);
                puffs.Add(sr);
                puffOffsets.Add(o);
            }
            edge = Art.Make("edge", transform, Art.Ring, new Color(0.75f, 0.72f, 0.7f, 0.35f), Vector2.zero, Vector2.one * Radius * 2f, 47);
            cloud.gameObject.SetActive(false);
            edge.enabled = false;
        }

        public bool Contains(Vector2 p) => Active && (p - Center).sqrMagnitude <= Radius * Radius;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            var lm = LevelManager.I;
            if (lm == null || lm.WaveNumber == 0) return;

            if (!Active)
            {
                timer -= dt;
                if (timer <= 0f) Begin(lm);
                return;
            }

            Vector3 p = transform.position + new Vector3(dir * Speed * dt, Mathf.Sin(t * 0.4f) * 0.3f * dt, 0f);
            transform.position = p;
            for (int i = 0; i < puffs.Count; i++)
            {
                Vector2 o = puffOffsets[i] + new Vector2(Mathf.Sin(t * 0.7f + i), Mathf.Cos(t * 0.5f + i * 1.3f)) * 0.4f;
                puffs[i].transform.localPosition = new Vector3(o.x, o.y, 0f);
            }
            fx -= dt;
            if (fx <= 0f)
            {
                fx = 0.08f;
                Vector2 q = Center + Random.insideUnitCircle * Radius;
                Fx.Burst(q, moteColor, 0.12f, 0.05f, 1.2f, Art.Circle, 49, new Vector3(dir * 2.5f, -0.4f, 0f));
            }
            if ((dir > 0f && p.x > 25f) || (dir < 0f && p.x < -25f))
            {
                Active = false;
                cloud.gameObject.SetActive(false);
                edge.enabled = false;
                timer = Interval;
            }
        }

        void Begin(LevelManager lm)
        {
            Active = true;
            dir = Random.value < 0.5f ? 1f : -1f;
            float y = Random.Range(-6f, 6f);
            transform.position = new Vector3(dir > 0f ? -25f : 25f, y, 0f);
            cloud.gameObject.SetActive(true);
            edge.enabled = true;
            AudioManager.Play(Sfx.Wind, 0.8f);
            lm.OnAshStorm();
        }
    }
}

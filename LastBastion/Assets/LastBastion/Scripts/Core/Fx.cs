using UnityEngine;

namespace LastBastion
{
    /// <summary>Short-lived visual effects: bursts, beams, rings.</summary>
    public class Fx : MonoBehaviour
    {
        SpriteRenderer sr;
        float life, age;
        Vector3 startScale, endScale, velocity;
        Color baseColor;

        public static void Burst(Vector3 pos, Color color, float startSize, float endSize, float life, Sprite sprite = null, int order = 45, Vector3 velocity = default(Vector3))
        {
            var r = Art.Make("fx", null, sprite != null ? sprite : Art.Glow, color, pos, Vector2.one * startSize, order);
            var fx = r.gameObject.AddComponent<Fx>();
            fx.Init(r, life, r.transform.localScale, new Vector3(endSize, endSize, 1f), velocity);
        }

        public static void Ring(Vector3 pos, Color color, float startRadius, float endRadius, float life, int order = 44)
        {
            Burst(pos, color, startRadius * 2f, endRadius * 2f, life, Art.Ring, order);
        }

        public static void Line(Vector2 a, Vector2 b, Color color, float width, float life, int order = 44)
        {
            var r = Art.Segment("fx-line", null, a, b, width, color, order);
            var fx = r.gameObject.AddComponent<Fx>();
            Vector3 s = r.transform.localScale;
            fx.Init(r, life, s, new Vector3(s.x, s.y * 0.2f, 1f), Vector3.zero);
        }

        void Init(SpriteRenderer r, float l, Vector3 s0, Vector3 s1, Vector3 v)
        {
            sr = r; life = Mathf.Max(0.01f, l); startScale = s0; endScale = s1; velocity = v; baseColor = r.color;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / life;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.Lerp(startScale, endScale, k);
            transform.position += velocity * Time.deltaTime;
            Color c = baseColor;
            c.a = baseColor.a * (1f - k);
            sr.color = c;
        }
    }
}

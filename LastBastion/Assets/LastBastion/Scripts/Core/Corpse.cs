using UnityEngine;

namespace LastBastion
{
    /// <summary>A defeated orc topples over, flattens and fades.</summary>
    public class Corpse : MonoBehaviour
    {
        SpriteRenderer sr;
        float age;
        Color baseColor;
        Vector3 baseScale;
        float spin;

        public static void Spawn(Vector3 pos, Color color, float radius)
        {
            var sr = Art.Make("corpse", null, Art.Circle, color, pos, Vector2.one * radius * 2f, 15);
            var c = sr.gameObject.AddComponent<Corpse>();
            c.sr = sr;
            c.baseColor = color;
            c.baseScale = sr.transform.localScale;
            c.spin = Random.value < 0.5f ? -1f : 1f;
            Art.Make("x1", sr.transform, Art.Square, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, new Vector2(0.5f, 0.08f), 16, 45f);
            Art.Make("x2", sr.transform, Art.Square, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, new Vector2(0.5f, 0.08f), 16, -45f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / 0.9f;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.localScale = new Vector3(baseScale.x * (1f + 0.3f * k), baseScale.y * (1f - 0.6f * Mathf.Min(1f, k * 3f)), 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, spin * 90f * Mathf.Min(1f, k * 4f));
            Color c = Color.Lerp(baseColor, new Color(0.15f, 0.12f, 0.1f, baseColor.a), 0.6f);
            c.a = 1f - k;
            sr.color = c;
            foreach (var child in GetComponentsInChildren<SpriteRenderer>())
                if (child != sr) child.color = new Color(0f, 0f, 0f, 0.6f * (1f - k));
        }
    }
}

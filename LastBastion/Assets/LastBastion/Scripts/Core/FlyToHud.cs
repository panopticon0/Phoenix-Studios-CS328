using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Coin / shard pickup: pops out of a defeated orc, hangs for a moment, then flies to its HUD
    /// counter (the gold total or the Dragonfire card) and calls back when it lands.
    /// </summary>
    public class FlyToHud : MonoBehaviour
    {
        Vector3 velocity;
        float age, hang, speed;
        System.Func<Vector2> guiTarget;
        System.Action onArrive;
        Vector3 baseScale;

        public static void Spawn(Vector3 pos, Color color, Color rim, float size, System.Func<Vector2> guiTarget, System.Action onArrive)
        {
            var root = new GameObject("pickup");
            root.transform.position = new Vector3(pos.x, pos.y, 0f);
            root.transform.localScale = Vector3.one * size;
            Art.Make("rim", root.transform, Art.Circle, rim, Vector2.zero, Vector2.one, 60);
            Art.Make("face", root.transform, Art.Circle, color, Vector2.zero, Vector2.one * 0.72f, 61);
            Art.Make("shine", root.transform, Art.Circle, new Color(1f, 1f, 1f, 0.7f), new Vector2(-0.15f, 0.15f), Vector2.one * 0.22f, 62);
            var f = root.AddComponent<FlyToHud>();
            Vector2 v = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.2f);
            f.velocity = new Vector3(v.x, v.y + 1.5f, 0f);
            f.hang = Random.Range(0.3f, 0.5f);
            f.guiTarget = guiTarget;
            f.onArrive = onArrive;
            f.baseScale = root.transform.localScale;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            if (age < hang)
            {
                transform.position += velocity * dt;
                velocity *= Mathf.Pow(0.02f, dt);
                return;
            }
            var lm = LevelManager.I;
            if (lm == null || lm.Cam == null || guiTarget == null) { Arrive(); return; }
            Vector3 target = UI.GuiToWorld(lm.Cam, guiTarget());
            speed += dt * 60f;
            Vector3 to = target - transform.position;
            to.z = 0f;
            float step = speed * dt;
            if (to.magnitude <= step || age > 3f) { Arrive(); return; }
            transform.position += to.normalized * step;
            transform.localScale = baseScale * Mathf.Lerp(1f, 0.7f, Mathf.Clamp01((age - hang) * 2f));
        }

        void Arrive()
        {
            if (onArrive != null) onArrive();
            Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace LastBastion
{
    /// <summary>Homing shots (arrows, bolts) and lobbed shots (boulders, meteors, Gorruk's axe).</summary>
    public class Projectile : MonoBehaviour
    {
        Enemy target;
        Vector3 lastTargetPos;
        float speed;
        System.Action<Enemy> onHit;

        bool lob;
        Vector3 start, end;
        float flight, age, arc, spin;
        System.Action<Vector3> onLand;
        Transform shadow;

        public static Projectile Homing(Vector3 from, Enemy target, float speed, Sprite sprite, Color color, Vector2 size, System.Action<Enemy> onHit)
        {
            var sr = Art.Make("projectile", null, sprite, color, from, size, 40);
            var p = sr.gameObject.AddComponent<Projectile>();
            p.target = target;
            p.lastTargetPos = target != null ? target.transform.position : from;
            p.speed = speed;
            p.onHit = onHit;
            return p;
        }

        public static Projectile Lob(Vector3 from, Vector3 to, float flight, float arc, Sprite sprite, Color color, Vector2 size, float spin, System.Action<Vector3> onLand)
        {
            var sr = Art.Make("lob", null, sprite, color, from, size, 41);
            var p = sr.gameObject.AddComponent<Projectile>();
            p.lob = true;
            p.start = new Vector3(from.x, from.y, 0f);
            p.end = new Vector3(to.x, to.y, 0f);
            p.flight = Mathf.Max(0.05f, flight);
            p.arc = arc;
            p.spin = spin;
            p.onLand = onLand;
            p.shadow = Art.Make("lobShadow", null, Art.Circle, new Color(0f, 0f, 0f, 0.35f), from, new Vector2(size.x, size.x * 0.6f), 39).transform;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (lob)
            {
                age += dt;
                float k = Mathf.Clamp01(age / flight);
                Vector3 ground = Vector3.Lerp(start, end, k);
                transform.position = ground + Vector3.up * arc * 4f * k * (1f - k);
                if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
                if (shadow != null) shadow.position = ground;
                if (k >= 1f)
                {
                    if (onLand != null) onLand(end);
                    Destroy(gameObject);
                }
                return;
            }

            if (target != null && target.Alive) lastTargetPos = target.transform.position;
            Vector3 to = lastTargetPos - transform.position;
            to.z = 0f;
            float step = speed * dt;
            if (to.magnitude <= step + 0.05f)
            {
                if (target != null && target.Alive && onHit != null) onHit(target);
                Destroy(gameObject);
                return;
            }
            Vector3 dir = to.normalized;
            transform.position += dir * step;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }
    }
}

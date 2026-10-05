using UnityEngine;

public class Projectile : MonoBehaviour
{
    Enemy target;
    float damage, splash, speed;
    Vector3 aim;

    public static void Spawn(Vector3 from, Enemy target, TowerData d)
    {
        var go = new GameObject("Projectile");
        go.transform.position = from;
        bool boulder = d.splash > 0;
        SpriteFactory.Make("Sprite", go.transform,
            SpriteFactory.Circle(boulder ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.9f, 0.85f, 0.6f)),
            Vector3.zero, Vector3.one * (boulder ? 0.25f : 0.12f), 4);

        var p = go.AddComponent<Projectile>();
        p.target = target;
        p.damage = d.damage;
        p.splash = d.splash;
        p.speed = d.projectileSpeed;
        p.aim = target.transform.position;
    }

    void Update()
    {
        if (target != null && !target.IsDead) aim = target.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);
        if ((transform.position - aim).sqrMagnitude < 0.0025f) Hit();
    }

    void Hit()
    {
        if (splash > 0f)
        {
            foreach (var e in Enemy.Active.ToArray())
                if (!e.IsDead && (e.transform.position - transform.position).sqrMagnitude <= splash * splash)
                    e.TakeDamage(damage);
        }
        else if (target != null)
        {
            target.TakeDamage(damage);
        }
        Destroy(gameObject);
    }
}

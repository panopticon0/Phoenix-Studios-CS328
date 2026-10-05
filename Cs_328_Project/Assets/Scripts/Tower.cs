using UnityEngine;

/// Plain data describing each tower type. Add new towers by adding entries here.
public class TowerData
{
    public string name;
    public int cost;
    public float range, fireRate, damage, splash, projectileSpeed;
    public Color color;

    public static readonly TowerData[] All =
    {
        new TowerData { name = "Archer Tower", cost = 50,  range = 2.5f, fireRate = 1.5f, damage = 8,
                        splash = 0f, projectileSpeed = 9f, color = new Color(0.55f, 0.35f, 0.15f) },
        new TowerData { name = "Catapult",     cost = 100, range = 3.5f, fireRate = 0.5f, damage = 25,
                        splash = 1f, projectileSpeed = 5f, color = new Color(0.30f, 0.20f, 0.10f) },
    };
}

public class Tower : MonoBehaviour
{
    TowerData data;
    Transform barrel;
    float cooldown;

    public static Tower Create(Vector3 pos, TowerData d)
    {
        var go = new GameObject(d.name);
        go.transform.position = pos;
        var t = go.AddComponent<Tower>();
        t.data = d;

        SpriteFactory.Make("Base", go.transform,
            SpriteFactory.Square(new Color(0.62f, 0.62f, 0.65f), null, true), Vector3.zero, Vector3.one * 0.9f, 2);
        t.barrel = SpriteFactory.Make("Barrel", go.transform,
            SpriteFactory.Square(new Color(0.2f, 0.12f, 0.05f), new Vector2(0, 0.5f)),
            Vector3.zero, new Vector3(0.5f, 0.12f, 1), 3).transform;
        SpriteFactory.Make("Hub", go.transform, SpriteFactory.Circle(d.color), Vector3.zero, Vector3.one * 0.45f, 4);
        return t;
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        var target = FindTarget();
        if (target == null) return;

        var dir = target.transform.position - transform.position;
        barrel.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        if (cooldown <= 0f)
        {
            Projectile.Spawn(transform.position, target, data);
            cooldown = 1f / data.fireRate;
        }
    }

    // Targets the enemy furthest along the road within range.
    Enemy FindTarget()
    {
        Enemy best = null;
        float bestProgress = float.MinValue;
        float r2 = data.range * data.range;
        foreach (var e in Enemy.Active)
        {
            if (e.IsDead) continue;
            if ((e.transform.position - transform.position).sqrMagnitude > r2) continue;
            if (e.PathProgress > bestProgress) { bestProgress = e.PathProgress; best = e; }
        }
        return best;
    }
}

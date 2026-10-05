using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Goblin, Orc }

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();

    const float BarWidth = 0.6f;

    public bool IsDead { get; private set; }

    /// Higher = further along the road. Towers use this to focus the lead enemy.
    public float PathProgress =>
        index * 100f - Vector3.Distance(transform.position, path[Mathf.Min(index, path.Length - 1)]);

    float speed, maxHealth, health;
    int reward, damage, index = 1;
    Vector3[] path;
    Transform bar;

    public static Enemy Spawn(EnemyKind kind, Vector3[] path)
    {
        var go = new GameObject(kind.ToString());
        go.transform.position = path[0];
        var e = go.AddComponent<Enemy>();
        e.path = path;

        Color color; float size;
        switch (kind)
        {
            case EnemyKind.Orc:
                color = new Color(0.20f, 0.42f, 0.20f); size = 0.75f;
                e.speed = 0.9f; e.maxHealth = 70; e.reward = 15; e.damage = 2;
                break;
            default: // Goblin
                color = new Color(0.60f, 0.80f, 0.30f); size = 0.5f;
                e.speed = 1.6f; e.maxHealth = 25; e.reward = 8; e.damage = 1;
                break;
        }
        e.health = e.maxHealth;

        SpriteFactory.Make("Body", go.transform, SpriteFactory.Circle(color), Vector3.zero, Vector3.one * size, 3);

        float y = size * 0.5f + 0.12f;
        SpriteFactory.Make("BarBG", go.transform, SpriteFactory.Square(Color.black),
            new Vector3(0, y, 0), new Vector3(BarWidth + 0.04f, 0.14f, 1), 5);
        e.bar = SpriteFactory.Make("BarFG", go.transform,
            SpriteFactory.Square(new Color(0.9f, 0.15f, 0.15f), new Vector2(0, 0.5f)),
            new Vector3(-BarWidth / 2f, y, 0), new Vector3(BarWidth, 0.1f, 1), 6).transform;
        return e;
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Update()
    {
        if (IsDead) return;
        var target = path[index];
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            index++;
            if (index >= path.Length)
            {
                IsDead = true;
                GameManager.Instance.LoseLife(damage);
                Destroy(gameObject);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        health -= amount;
        bar.localScale = new Vector3(BarWidth * Mathf.Clamp01(health / maxHealth), 0.1f, 1);
        if (health <= 0)
        {
            IsDead = true;
            GameManager.Instance.AddGold(reward);
            Destroy(gameObject);
        }
    }
}

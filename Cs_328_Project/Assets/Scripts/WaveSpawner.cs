using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    class Group
    {
        public EnemyKind kind; public int count; public float interval;
        public Group(EnemyKind k, int c, float i) { kind = k; count = c; interval = i; }
    }

    // Edit this to tune the level's difficulty.
    readonly Group[][] waves =
    {
        new[] { new Group(EnemyKind.Goblin, 8, 1.2f) },
        new[] { new Group(EnemyKind.Goblin, 12, 0.9f) },
        new[] { new Group(EnemyKind.Goblin, 8, 0.8f), new Group(EnemyKind.Orc, 4, 2f) },
        new[] { new Group(EnemyKind.Orc, 8, 1.5f) },
        new[] { new Group(EnemyKind.Goblin, 15, 0.5f), new Group(EnemyKind.Orc, 10, 1.2f) },
    };

    IEnumerator Start()
    {
        var gm = GameManager.Instance;
        gm.TotalWaves = waves.Length;
        yield return new WaitForSeconds(5f); // time to place your first towers

        for (int w = 0; w < waves.Length; w++)
        {
            gm.Wave = w + 1;
            foreach (var g in waves[w])
                for (int i = 0; i < g.count; i++)
                {
                    Enemy.Spawn(g.kind, MapBuilder.Instance.Waypoints);
                    yield return new WaitForSeconds(g.interval);
                }

            while (Enemy.Active.Count > 0) yield return null;
            if (w < waves.Length - 1) yield return new WaitForSeconds(6f);
        }
        gm.Victory();
    }
}

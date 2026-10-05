using System.Collections.Generic;
using UnityEngine;

/// Builds Level 1: a 16x9 grid with a winding dirt road from the forest (left) to your castle (right).
public class MapBuilder : MonoBehaviour
{
    public static MapBuilder Instance { get; private set; }

    public const int Width = 16, Height = 9;

    // Corners of the road in grid cells ((0,0) = bottom-left). Edit these to reshape the level.
    static readonly Vector2Int[] Corners =
    {
        new(0, 7), new(3, 7), new(3, 2), new(7, 2), new(7, 6), new(11, 6), new(11, 1), new(15, 1)
    };

    enum Cell { Grass, Road, Tree }

    readonly Cell[,] cells = new Cell[Width, Height];
    readonly bool[,] occupied = new bool[Width, Height];

    /// World-space points enemies walk through. The last one is the castle.
    public Vector3[] Waypoints { get; private set; }

    void Awake()
    {
        Instance = this;
        Build();
    }

    void Build()
    {
        // 1. Lay the road
        for (int i = 0; i < Corners.Length - 1; i++)
        {
            var a = Corners[i];
            var b = Corners[i + 1];
            var step = new Vector2Int(System.Math.Sign(b.x - a.x), System.Math.Sign(b.y - a.y));
            var p = a;
            cells[p.x, p.y] = Cell.Road;
            while (p != b) { p += step; cells[p.x, p.y] = Cell.Road; }
        }

        // 2. Scatter some trees (blocked tiles)
        var rng = new System.Random(7);
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (cells[x, y] == Cell.Grass && rng.NextDouble() < 0.10)
                    cells[x, y] = Cell.Tree;

        // 3. Draw tiles
        var grassA = new Color(0.36f, 0.58f, 0.25f);
        var grassB = new Color(0.33f, 0.54f, 0.23f);
        var road = new Color(0.62f, 0.47f, 0.30f);
        var tree = new Color(0.10f, 0.32f, 0.13f);

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                bool isRoad = cells[x, y] == Cell.Road;
                var col = isRoad ? road : ((x + y) % 2 == 0 ? grassA : grassB);
                SpriteFactory.Make($"Tile {x},{y}", transform, SpriteFactory.Square(col, null, !isRoad),
                                   CellToWorld(x, y), Vector3.one, 0);
                if (cells[x, y] == Cell.Tree)
                    SpriteFactory.Make("Tree", transform, SpriteFactory.Circle(tree),
                                       CellToWorld(x, y), Vector3.one * 0.8f, 1);
            }

        // 4. Castle at the end of the road
        var end = Corners[Corners.Length - 1];
        SpriteFactory.Make("Castle", transform,
            SpriteFactory.Square(new Color(0.55f, 0.55f, 0.6f), null, true),
            CellToWorld(end.x, end.y), Vector3.one * 0.9f, 1);
        SpriteFactory.Make("Banner", transform, SpriteFactory.Square(new Color(0.7f, 0.1f, 0.1f)),
            CellToWorld(end.x, end.y) + new Vector3(0, 0.2f, 0), new Vector3(0.15f, 0.3f, 1), 2);

        // 5. Waypoints (start one tile off-screen so enemies walk in)
        var pts = new List<Vector3> { CellToWorld(-1, Corners[0].y) };
        foreach (var c in Corners) pts.Add(CellToWorld(c.x, c.y));
        Waypoints = pts.ToArray();
    }

    public static Vector3 CellToWorld(int x, int y) =>
        new Vector3(x - Width / 2f + 0.5f, y - Height / 2f + 0.5f, 0);

    public bool WorldToCell(Vector3 w, out int x, out int y)
    {
        x = Mathf.FloorToInt(w.x + Width / 2f);
        y = Mathf.FloorToInt(w.y + Height / 2f);
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public bool CanBuild(int x, int y) => cells[x, y] == Cell.Grass && !occupied[x, y];
    public void SetOccupied(int x, int y) => occupied[x, y] = true;
}

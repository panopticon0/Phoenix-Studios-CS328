using System.Collections.Generic;
using UnityEngine;

/// Generates simple placeholder sprites in code so the project needs no art.
/// Replace these with real sprites later.
public static class SpriteFactory
{
    static readonly Dictionary<string, Sprite> cache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => cache.Clear();

    public static Sprite Square(Color c, Vector2? pivot = null, bool border = false)
    {
        string key = $"sq{ColorUtility.ToHtmlStringRGBA(c)}{pivot}{border}";
        if (cache.TryGetValue(key, out var s)) return s;

        const int size = 16;
        var tex = new Texture2D(size, size) { filterMode = FilterMode.Point };
        var dark = Color.Lerp(c, Color.black, 0.25f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = border && (x == 0 || y == 0 || x == size - 1 || y == size - 1);
                tex.SetPixel(x, y, edge ? dark : c);
            }
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), pivot ?? new Vector2(0.5f, 0.5f), size);
        cache[key] = s;
        return s;
    }

    /// Circle with a diameter of exactly 1 world unit.
    public static Sprite Circle(Color c)
    {
        string key = $"ci{ColorUtility.ToHtmlStringRGBA(c)}";
        if (cache.TryGetValue(key, out var s)) return s;

        const int size = 64;
        var tex = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
        float center = (size - 1) / 2f, r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                tex.SetPixel(x, y, d <= r - 1 ? c : Color.clear);
            }
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        cache[key] = s;
        return s;
    }

    public static SpriteRenderer Make(string name, Transform parent, Sprite sprite,
                                      Vector3 localPos, Vector3 scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }
}

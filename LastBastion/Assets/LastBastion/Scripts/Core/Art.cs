using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>Colour palette from the GDD (orc rust/iron, Warden stone and ember).</summary>
    public static class Palette
    {
        public static readonly Color Ink = Hex("#15110E");
        public static readonly Color Night = Hex("#1A1512");
        public static readonly Color PanelFill = new Color(0.10f, 0.08f, 0.07f, 0.94f);
        public static readonly Color PanelBorder = Hex("#5A4636");
        public static readonly Color Parchment = Hex("#F3EBDD");
        public static readonly Color Muted = Hex("#CFC3B0");
        public static readonly Color Dim = Hex("#A89A86");
        public static readonly Color Ember = Hex("#E0782E");
        public static readonly Color EmberDeep = Hex("#A34A12");
        public static readonly Color Gold = Hex("#E8C547");
        public static readonly Color Good = Hex("#7FC36A");
        public static readonly Color Bad = Hex("#D9533B");
        public static readonly Color Magic = Hex("#B48CD9");
        public static readonly Color Frost = Hex("#7FC8E8");
        public static readonly Color Undead = Hex("#8FBF6A");

        // World
        public static readonly Color Ground = Hex("#3B302A");
        public static readonly Color GroundDark = Hex("#2A221D");
        public static readonly Color Rock = Hex("#4E423A");
        public static readonly Color Road = Hex("#8A6F52");
        public static readonly Color RoadEdge = Hex("#5E4A38");
        public static readonly Color Stone = Hex("#9A8F80");
        public static readonly Color StoneDark = Hex("#5A5048");
        public static readonly Color OrcSkin = Hex("#6E8A3A");
        public static readonly Color OrcArmor = Hex("#A0442A");
        public static readonly Color Iron = Hex("#6A6560");
        public static readonly Color HeroBlue = Hex("#2F5FA8");

        // Upgrade branches
        public static readonly Color BranchPower = Hex("#E0782E");
        public static readonly Color BranchSpeed = Hex("#E8C547");
        public static readonly Color BranchUtility = Hex("#B48CD9");

        public static Color Branch(int b) => b == 0 ? BranchPower : b == 1 ? BranchSpeed : BranchUtility;

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }

    /// <summary>
    /// All visuals are generated at runtime (no imported art), so the project works the moment
    /// the scripts are dropped in. Swap these sprites for real art later.
    /// </summary>
    public static class Art
    {
        static Sprite circle, ring, glow, square, triangle;
        static Material spriteMaterial;
        static bool materialLoaded;

        public static Sprite Circle { get { if (circle == null) circle = MakeRadial(128, 0); return circle; } }
        public static Sprite Ring { get { if (ring == null) ring = MakeRadial(256, 1); return ring; } }
        public static Sprite Glow { get { if (glow == null) glow = MakeRadial(128, 2); return glow; } }
        public static Sprite Square { get { if (square == null) square = MakeSquare(); return square; } }
        public static Sprite Triangle { get { if (triangle == null) triangle = MakeTriangle(128); return triangle; } }

        /// <summary>Unlit sprite material created by the editor setup (works in URP and Built-in).</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (!materialLoaded)
                {
                    materialLoaded = true;
                    spriteMaterial = Resources.Load<Material>("LB_SpriteUnlit");
                }
                return spriteMaterial;
            }
        }

        static Sprite MakeRadial(int size, int mode)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            float r = size * 0.5f;
            float ringWidth = size * 0.035f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (mode == 0) a = Mathf.Clamp01(r - 1f - d);
                    else if (mode == 1) a = Mathf.Clamp01(ringWidth - Mathf.Abs(d - (r - ringWidth - 1.5f)));
                    else { float t = Mathf.Clamp01(1f - d / r); a = t * t; }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        static Sprite MakeTriangle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float t = (y + 0.5f) / size;
                float half = (1f - t) * r;
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - r) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>Create a sprite object. Sprites are 1 world unit wide, so size = world size.</summary>
        public static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, int order, float rotation = 0f)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            if (rotation != 0f) go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (SpriteMaterial != null) sr.sharedMaterial = SpriteMaterial;
            return sr;
        }

        /// <summary>A rectangle stretched between two points (roads, beams, telegraph lines).</summary>
        public static SpriteRenderer Segment(string name, Transform parent, Vector2 a, Vector2 b, float width, Color color, int order)
        {
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return Make(name, parent, Square, color, (a + b) * 0.5f, new Vector2(d.magnitude, width), order, angle);
        }

        public static Camera MakeCamera(Color background, float size)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
            return cam;
        }
    }

    /// <summary>World-space health bar made from two sprites.</summary>
    public class HealthBar
    {
        readonly GameObject root;
        readonly Transform fill;
        readonly SpriteRenderer fillRenderer;
        readonly float width;
        float frac = 1f;
        bool visible = true;
        public bool AlwaysShow;

        public HealthBar(Transform parent, float width, float yOffset, Color color)
        {
            this.width = width;
            root = new GameObject("HealthBar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, yOffset, 0f);
            Art.Make("bg", root.transform, Art.Square, new Color(0f, 0f, 0f, 0.75f), Vector2.zero, new Vector2(width + 0.08f, 0.18f), 50);
            fillRenderer = Art.Make("fill", root.transform, Art.Square, color, Vector2.zero, new Vector2(width, 0.11f), 51);
            fill = fillRenderer.transform;
            Refresh();
        }

        public void Set(float f)
        {
            frac = Mathf.Clamp01(f);
            fill.localScale = new Vector3(width * frac, 0.11f, 1f);
            fill.localPosition = new Vector3(-width * (1f - frac) * 0.5f, 0f, 0f);
            Refresh();
        }

        public void SetColor(Color c) { fillRenderer.color = c; }
        public void Show(bool on) { visible = on; Refresh(); }

        void Refresh()
        {
            if (root != null) root.SetActive(visible && (AlwaysShow || frac < 0.999f));
        }
    }
}

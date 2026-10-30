using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    public enum BtnKind { Normal, Accent, Danger, Ghost }

    /// <summary>
    /// Small immediate-mode UI kit. Everything is laid out on a 1080-pixel-tall reference canvas
    /// and scaled to the real screen, so the HUD looks the same at any resolution.
    /// </summary>
    public static class UI
    {
        public const float RefHeight = 1080f;
        public static float Scale => Mathf.Max(0.01f, Screen.height / RefHeight);
        public static float W => Screen.width / Scale;
        public static float H => RefHeight;

        static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        static bool overThisFrame;

        /// <summary>True when the mouse was over a HUD panel last frame (world clicks are ignored).</summary>
        public static bool PointerOverUI { get; private set; }
        public static bool Busy => PointerOverUI || GUIUtility.hotControl != 0;

        public static void Begin()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
        }

        public static void FrameStart()
        {
            if (Event.current.type == EventType.Repaint) overThisFrame = false;
        }

        public static void FrameEnd()
        {
            if (Event.current.type == EventType.Repaint) PointerOverUI = overThisFrame;
        }

        /// <summary>Register a rect as UI so clicks inside it don't reach the game world.</summary>
        public static Rect Block(Rect r)
        {
            if (Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition)) overThisFrame = true;
            return r;
        }

        public static Vector2 WorldToGUI(Camera cam, Vector3 world)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            return new Vector2(s.x / Scale, (Screen.height - s.y) / Scale);
        }

        /// <summary>Cached text style. Rich text is on, so &lt;b&gt; and &lt;color&gt; tags work.</summary>
        public static GUIStyle S(int size, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle font = FontStyle.Normal, Color? color = null, bool wrap = false)
        {
            Color c = color ?? Palette.Parchment;
            string key = size + "|" + (int)anchor + "|" + (int)font + "|" + c.r.ToString("0.000") + c.g.ToString("0.000") + c.b.ToString("0.000") + c.a.ToString("0.000") + "|" + wrap;
            GUIStyle st;
            if (styles.TryGetValue(key, out st) && st != null) return st;
            st = new GUIStyle();
            st.fontSize = size;
            st.alignment = anchor;
            st.fontStyle = font;
            st.wordWrap = wrap;
            st.richText = true;
            st.clipping = TextClipping.Overflow;
            st.normal.textColor = c;
            st.hover.textColor = c;
            st.active.textColor = c;
            st.focused.textColor = c;
            st.onNormal.textColor = c;
            st.onHover.textColor = c;
            styles[key] = st;
            return st;
        }

        public static void Rounded(Rect r, Color c, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        public static void Outline(Rect r, Color c, float width, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
        }

        public static void Fill(Rect r, Color c) { Rounded(r, c, 0f); }

        public static void Disc(Rect r, Color c) { Rounded(r, c, Mathf.Min(r.width, r.height) * 0.5f); }

        public static void Circle(Rect r, Color c, float width) { Outline(r, c, width, Mathf.Min(r.width, r.height) * 0.5f); }

        public static void Panel(Rect r, bool highlighted = false)
        {
            Rounded(r, Palette.PanelFill, 12f);
            Outline(r, highlighted ? Palette.Ember : Palette.PanelBorder, highlighted ? 3f : 2f, 12f);
        }

        public static void Bar(Rect r, float frac, Color fill, Color back)
        {
            Rounded(r, back, r.height * 0.5f);
            frac = Mathf.Clamp01(frac);
            if (frac > 0.001f) Rounded(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * frac), r.height), fill, r.height * 0.5f);
        }

        public static void Label(Rect r, string text, GUIStyle style) { GUI.Label(r, text, style); }

        public static void Shadowed(Rect r, string text, GUIStyle style, float offset = 4f)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.65f * c.a);
            GUI.Label(new Rect(r.x + offset, r.y + offset, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        public static bool Button(Rect r, string text, BtnKind kind = BtnKind.Normal, int fontSize = 26)
        {
            bool enabled = GUI.enabled;
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            Color fill, border, ink;
            switch (kind)
            {
                case BtnKind.Accent:
                    fill = hover ? Palette.Hex("#F08A3E") : Palette.Ember;
                    border = Palette.Hex("#FFC48A");
                    ink = Palette.Ink;
                    break;
                case BtnKind.Danger:
                    fill = hover ? Palette.Hex("#8E3A28") : Palette.Hex("#6E2A1C");
                    border = Palette.Bad;
                    ink = Palette.Parchment;
                    break;
                case BtnKind.Ghost:
                    fill = hover ? new Color(1f, 1f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.03f);
                    border = hover ? Palette.Ember : Palette.PanelBorder;
                    ink = Palette.Parchment;
                    break;
                default:
                    fill = hover ? Palette.Hex("#4A3628") : Palette.Hex("#33261E");
                    border = hover ? Palette.Ember : Palette.Hex("#6E5442");
                    ink = Palette.Parchment;
                    break;
            }
            if (!enabled)
            {
                fill = Palette.Hex("#241C17");
                border = Palette.Hex("#3A2E26");
                ink = Palette.Hex("#6E6258");
            }
            Rounded(r, border, 10f);
            Rounded(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), fill, 8f);
            return GUI.Button(r, text, S(fontSize, TextAnchor.MiddleCenter, FontStyle.Bold, ink, true));
        }

        /// <summary>Dark full-screen veil for modal overlays; also blocks world clicks.</summary>
        public static void Veil(float alpha = 0.6f)
        {
            Rect r = new Rect(0f, 0f, W, H);
            Block(r);
            Fill(r, new Color(0f, 0f, 0f, alpha));
        }
    }
}

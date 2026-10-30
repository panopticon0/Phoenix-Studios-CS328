using UnityEngine;

namespace LastBastion
{
    /// <summary>A stone build site next to the road. Click it to open the build menu.</summary>
    public class BuildPlot : MonoBehaviour
    {
        public Tower Tower;
        SpriteRenderer ring, highlight;
        float t;

        public Vector2 Position => transform.position;

        public static BuildPlot Create(Transform parent, Vector2 pos)
        {
            var go = new GameObject("BuildPlot");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var plot = go.AddComponent<BuildPlot>();
            plot.Build();
            return plot;
        }

        void Build()
        {
            Art.Make("shadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.06f, -0.08f), new Vector2(1.45f, 1.45f), 7);
            Art.Make("base", transform, Art.Circle, Palette.StoneDark, Vector2.zero, new Vector2(1.35f, 1.35f), 8);
            Art.Make("inner", transform, Art.Circle, Palette.Hex("#463C35"), Vector2.zero, new Vector2(1.05f, 1.05f), 9);
            ring = Art.Make("ring", transform, Art.Ring, Palette.WithAlpha(Palette.Ember, 0.45f), Vector2.zero, new Vector2(1.3f, 1.3f), 10);
            highlight = Art.Make("highlight", transform, Art.Ring, Palette.Parchment, Vector2.zero, new Vector2(1.75f, 1.75f), 17);
            highlight.enabled = false;
        }

        public void SetSelected(bool selected)
        {
            if (highlight != null) highlight.enabled = selected;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            bool empty = Tower == null;
            ring.enabled = empty;
            if (empty) ring.color = Palette.WithAlpha(Palette.Ember, 0.3f + 0.2f * Mathf.Sin(t * 2.5f));
            if (highlight.enabled) highlight.transform.localScale = Vector3.one * (1.75f + 0.08f * Mathf.Sin(t * 6f));
        }
    }
}

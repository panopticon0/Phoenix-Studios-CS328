using UnityEngine;

/// Handles tower selection (keys 1/2), the hover preview, and placing towers by clicking.
public class BuildManager : MonoBehaviour
{
    int selected;
    SpriteRenderer hover, ring;

    void Awake()
    {
        ring = SpriteFactory.Make("RangeRing", transform, SpriteFactory.Circle(Color.white), Vector3.zero, Vector3.one, 6);
        hover = SpriteFactory.Make("Hover", transform, SpriteFactory.Square(Color.white), Vector3.zero, Vector3.one, 7);
        SetPreview(false);
    }

    void SetPreview(bool on)
    {
        hover.gameObject.SetActive(on);
        ring.gameObject.SetActive(on);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.GameOver) { SetPreview(false); return; }

        if (Input.GetKeyDown(KeyCode.Alpha1)) selected = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selected = 1;
        var data = TowerData.All[selected];

        var world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (!MapBuilder.Instance.WorldToCell(world, out int x, out int y)) { SetPreview(false); return; }

        bool ok = MapBuilder.Instance.CanBuild(x, y) && gm.Gold >= data.cost;
        var pos = MapBuilder.CellToWorld(x, y);

        SetPreview(true);
        hover.transform.position = pos;
        hover.color = ok ? new Color(0.2f, 1f, 0.2f, 0.4f) : new Color(1f, 0.2f, 0.2f, 0.4f);
        ring.transform.position = pos;
        ring.transform.localScale = Vector3.one * data.range * 2f;
        ring.color = new Color(1, 1, 1, 0.12f);

        if (Input.GetMouseButtonDown(0) && ok && gm.TrySpend(data.cost))
        {
            Tower.Create(pos, data);
            MapBuilder.Instance.SetOccupied(x, y);
        }
    }

    void OnGUI()
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        for (int i = 0; i < TowerData.All.Length; i++)
        {
            var t = TowerData.All[i];
            s.normal.textColor = i == selected ? Color.yellow : Color.white;
            GUI.Label(new Rect(10, Screen.height - 70 + i * 28, 500, 28),
                      $"{(i == selected ? "> " : "  ")}[{i + 1}] {t.name} - {t.cost}g", s);
        }
    }
}

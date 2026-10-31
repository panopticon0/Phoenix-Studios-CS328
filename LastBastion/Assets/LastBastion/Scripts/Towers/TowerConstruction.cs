using System.Collections;
using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Tower building animation. When a tower is bought, a Warden appears beside the plot and swings
    /// a hammer. At the moment of impact the magic particles burst and the tower pops into place;
    /// the Warden leaves when the animation finishes.
    ///
    /// Uses the team's animated prefab if it is at Assets/LastBastion/Resources/Warden_Build.prefab
    /// (its Animator plays on spawn, and its child ParticleSystem is fired at impact). If the prefab
    /// isn't there, a code-drawn Warden plays the same timing so the game still works.
    /// </summary>
    public class TowerConstruction : MonoBehaviour
    {
        public const string WardenPrefabResource = "Warden_Build";
        public const float ImpactTime = 0.35f;    // when the hammer hits (matches the Warden animation)
        public const float BuildDuration = 0.9f;  // total length before the Warden is removed
        static readonly Vector3 WardenOffset = new Vector3(-0.5f, 0f, 0f);

        static GameObject wardenPrefab;
        static bool prefabLoaded;

        BuildPlot plot;
        TowerType type;

        static GameObject WardenPrefab
        {
            get
            {
                if (!prefabLoaded)
                {
                    prefabLoaded = true;
                    wardenPrefab = Resources.Load<GameObject>(WardenPrefabResource);
                }
                return wardenPrefab;
            }
        }

        public static void Begin(BuildPlot plot, TowerType type)
        {
            var go = new GameObject("Construction - " + TowerData.Get(type).Name);
            go.transform.position = plot.transform.position;
            var c = go.AddComponent<TowerConstruction>();
            c.plot = plot;
            c.type = type;
            c.StartCoroutine(c.Run());
        }

        IEnumerator Run()
        {
            Vector3 pos = plot.transform.position;
            var prefab = WardenPrefab;

            GameObject warden = null;
            ParticleSystem particles = null;
            Transform hammerPivot = null;

            // Scaffold outline while the tower is being built
            var scaffold = Art.Make("scaffold", transform, Art.Square, new Color(0.55f, 0.4f, 0.25f, 0.0f), Vector2.zero, new Vector2(1.1f, 1.1f), 11);

            if (prefab != null)
            {
                warden = Instantiate(prefab, pos + WardenOffset, Quaternion.identity);
                particles = warden.GetComponentInChildren<ParticleSystem>(true);
                if (particles != null)
                {
                    // Draw the burst above the map and towers.
                    var pr = particles.GetComponent<ParticleSystemRenderer>();
                    if (pr != null) pr.sortingOrder = 45;
                }
                // If WardenBuildEffect is on the prefab, let this script control timing instead.
                foreach (var mb in warden.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == "WardenBuildEffect") mb.enabled = false;
            }
            else
            {
                warden = BuildFallbackWarden(pos + WardenOffset, out hammerPivot);
            }

            // Wind-up until the hammer strikes
            float t = 0f;
            while (t < ImpactTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / ImpactTime);
                scaffold.color = new Color(0.55f, 0.4f, 0.25f, 0.6f * k);
                if (hammerPivot != null)
                {
                    // Raise over the first 70%, then swing down hard.
                    float angle = k < 0.7f ? Mathf.Lerp(0f, 110f, k / 0.7f) : Mathf.Lerp(110f, -20f, (k - 0.7f) / 0.3f);
                    hammerPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
                yield return null;
            }

            // Impact: particles, dust, and the tower appears
            if (particles != null)
            {
                particles.gameObject.SetActive(true);
                particles.Play(true);
            }
            else
            {
                for (int i = 0; i < 14; i++)
                {
                    Vector2 dir = Random.insideUnitCircle.normalized;
                    Fx.Burst(pos, new Color(0.45f, 0.95f, 1f, 0.9f), 0.25f, 0.05f, Random.Range(0.35f, 0.6f), Art.Circle, 46, new Vector3(dir.x, dir.y, 0f) * Random.Range(1.5f, 3f));
                }
            }
            Fx.Ring(pos, new Color(0.6f, 0.95f, 1f, 0.8f), 0.2f, 1.1f, 0.3f);
            Destroy(scaffold.gameObject);

            if (LevelManager.I != null) LevelManager.I.FinishBuild(plot, type);
            else if (plot != null) plot.Building = false;

            // Follow-through for the rest of the animation
            t = 0f;
            float rest = Mathf.Max(0f, BuildDuration - ImpactTime);
            while (t < rest)
            {
                t += Time.deltaTime;
                if (hammerPivot != null) hammerPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-20f, 0f, Mathf.Clamp01(t / 0.2f)));
                yield return null;
            }

            if (warden != null) Destroy(warden);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Scene change mid-build: don't leave the plot locked.
            if (plot != null && plot.Building && plot.Tower == null) plot.Building = false;
        }

        /// <summary>Code-drawn Warden with a swinging hammer, used when the prefab is missing.</summary>
        static GameObject BuildFallbackWarden(Vector3 at, out Transform hammerPivot)
        {
            var root = new GameObject("Warden (placeholder)");
            root.transform.position = at;
            Art.Make("shadow", root.transform, Art.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.04f, -0.1f), new Vector2(0.6f, 0.38f), 20);
            Art.Make("body", root.transform, Art.Circle, Palette.Hex("#3F6E8F"), Vector2.zero, new Vector2(0.5f, 0.5f), 21);
            Art.Make("apron", root.transform, Art.Circle, Palette.Hex("#8A5A32"), new Vector2(0f, -0.06f), new Vector2(0.32f, 0.26f), 22);
            Art.Make("head", root.transform, Art.Circle, Palette.Hex("#E3C4A0"), new Vector2(0f, 0.2f), new Vector2(0.26f, 0.26f), 23);

            hammerPivot = new GameObject("HammerPivot").transform;
            hammerPivot.SetParent(root.transform, false);
            hammerPivot.localPosition = new Vector3(0.18f, 0.05f, 0f);
            Art.Make("handle", hammerPivot, Art.Square, Palette.Hex("#6B4A2A"), new Vector2(0.22f, 0f), new Vector2(0.45f, 0.06f), 24);
            Art.Make("hammerHead", hammerPivot, Art.Square, Palette.Hex("#B8B2AA"), new Vector2(0.44f, 0f), new Vector2(0.14f, 0.24f), 25);
            return root;
        }
    }
}

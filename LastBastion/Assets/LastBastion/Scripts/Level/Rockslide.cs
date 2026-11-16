using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Hazard: the Rockslide Cliff above the Ashen Pass.
    /// Unique mechanic: the PLAYER triggers it. Click the cracked cliff (or press R) and three boulders
    /// thunder down a fixed lane across the road, crushing and stunning every orc they hit (the stun
    /// interrupts boss wind-ups), but they also flatten Sir Aldric if he stands in the lane.
    /// It then needs 40 seconds to rebuild. Timing it is the skill.
    /// </summary>
    public class Rockslide : MonoBehaviour
    {
        public enum Phase { Ready, Rumbling, Rolling, Recharging }

        public const float LaneHalfWidth = 0.95f;
        public Phase Current { get; private set; }
        public float RechargeLeft { get; private set; }
        public float RechargeFrac => Current == Phase.Recharging ? 1f - RechargeLeft / Balance.RockslideRecharge : Current == Phase.Ready ? 1f : 0f;
        public bool Ready => Current == Phase.Ready;
        public Vector2 Position => transform.position;
        public Vector2 LaneFrom { get; private set; }
        public Vector2 LaneTo { get; private set; }
        /// <summary>"Rockslide" on the Ashen Pass, "Deadfall" (a rotten tree dropping logs) in the Weeping Marsh.</summary>
        public string DisplayName { get; private set; }
        bool marsh;

        float timer, t;
        int boulders;
        Vector3 basePos;
        SpriteRenderer glowRing, pile1, pile2, pile3, clock;

        public static Rockslide Create(Transform parent, Vector2 cliff, Vector2 from, Vector2 to)
        {
            var go = new GameObject("Rockslide Cliff");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(cliff.x, cliff.y, 0f);
            var r = go.AddComponent<Rockslide>();
            r.LaneFrom = from;
            r.LaneTo = to;
            r.basePos = go.transform.position;
            var lmT = LevelManager.I;
            r.marsh = lmT != null && lmT.Level != null && lmT.Level.Theme == LevelTheme.Marsh;
            r.DisplayName = r.marsh ? "Deadfall" : "Rockslide";
            go.name = r.DisplayName;
            r.Build(parent);
            r.Current = Phase.Ready;
            return r;
        }

        void Build(Transform parent)
        {
            // The lane: a scarred track down the slope.
            Vector2 d = LaneTo - LaneFrom;
            int marks = Mathf.CeilToInt(d.magnitude / 0.9f);
            for (int i = 0; i < marks; i++)
            {
                Vector2 p = Vector2.Lerp(LaneFrom, LaneTo, (i + 0.5f) / marks);
                Art.Make("laneMark", parent, Art.Square, new Color(0.15f, 0.1f, 0.08f, 0.35f), p + new Vector2(-0.55f, 0f), new Vector2(0.12f, 0.45f), 4);
                Art.Make("laneMark", parent, Art.Square, new Color(0.15f, 0.1f, 0.08f, 0.35f), p + new Vector2(0.55f, 0f), new Vector2(0.12f, 0.45f), 4);
            }
            Art.Segment("lane", parent, LaneFrom, LaneTo, LaneHalfWidth * 2f, new Color(0.35f, 0.25f, 0.18f, 0.18f), 4);

            if (marsh)
            {
                // A huge rotten tree leaning over the road, with a stack of logs at its foot.
                Art.Make("treeShadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.2f, -0.3f), new Vector2(3.6f, 2.4f), 5);
                Art.Make("roots", transform, Art.Circle, Palette.Hex("#2E2418"), new Vector2(0f, 0.1f), new Vector2(2.6f, 1.6f), 6);
                Art.Make("trunk", transform, Art.Circle, Palette.Hex("#4A3A28"), new Vector2(0f, 0.3f), new Vector2(1.5f, 1.5f), 7);
                Art.Make("trunkRing", transform, Art.Ring, Palette.Hex("#2E2418"), new Vector2(0f, 0.3f), new Vector2(1.1f, 1.1f), 8);
                Art.Make("branchL", transform, Art.Square, Palette.Hex("#3A2E20"), new Vector2(-1.1f, 0.6f), new Vector2(1.6f, 0.18f), 7, 25f);
                Art.Make("branchR", transform, Art.Square, Palette.Hex("#3A2E20"), new Vector2(1.1f, 0.7f), new Vector2(1.5f, 0.16f), 7, -30f);
                Art.Make("moss", transform, Art.Circle, Palette.Hex("#4E6A3A"), new Vector2(-0.4f, 0.55f), new Vector2(0.6f, 0.35f), 8);
                pile1 = Art.Make("log", transform, Art.Square, Palette.Hex("#5E4A32"), new Vector2(-0.1f, -0.6f), new Vector2(1.4f, 0.32f), 9);
                pile2 = Art.Make("log", transform, Art.Square, Palette.Hex("#6E583C"), new Vector2(0.1f, -0.92f), new Vector2(1.3f, 0.3f), 9);
                pile3 = Art.Make("log", transform, Art.Square, Palette.Hex("#56442E"), new Vector2(0f, -0.3f), new Vector2(1.2f, 0.3f), 10);
                glowRing = Art.Make("readyRing", transform, Art.Ring, Palette.HeroGlow, new Vector2(0f, -0.4f), Vector2.one * 2.4f, 11);
                clock = Art.Make("rechargeClock", transform, Art.Radial(0f), new Color(1f, 0.82f, 0.35f, 0.75f), new Vector2(0f, 0.2f), Vector2.one * 0.8f, 12);
                clock.enabled = false;
                return;
            }

            // The cliff
            Art.Make("cliffShadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.15f, -0.25f), new Vector2(4.4f, 2.6f), 5);
            Art.Make("cliffA", transform, Art.Circle, Palette.Hex("#3A302A"), new Vector2(-0.9f, 0f), new Vector2(2.4f, 2f), 6);
            Art.Make("cliffB", transform, Art.Circle, Palette.Hex("#463A32"), new Vector2(0.9f, 0.1f), new Vector2(2.6f, 2.1f), 6);
            Art.Make("cliffC", transform, Art.Circle, Palette.Hex("#52453B"), new Vector2(0f, 0.3f), new Vector2(2.2f, 1.8f), 7);
            Art.Make("crack1", transform, Art.Square, Palette.Hex("#1E1612"), new Vector2(-0.3f, -0.1f), new Vector2(1.2f, 0.07f), 8, 25f);
            Art.Make("crack2", transform, Art.Square, Palette.Hex("#1E1612"), new Vector2(0.4f, 0.1f), new Vector2(0.9f, 0.07f), 8, -35f);
            pile1 = Art.Make("boulder", transform, Art.Circle, Palette.Hex("#7A6E64"), new Vector2(-0.45f, -0.6f), Vector2.one * 0.75f, 9);
            pile2 = Art.Make("boulder", transform, Art.Circle, Palette.Hex("#6E625A"), new Vector2(0.35f, -0.7f), Vector2.one * 0.65f, 9);
            pile3 = Art.Make("boulder", transform, Art.Circle, Palette.Hex("#857A70"), new Vector2(-0.05f, -0.3f), Vector2.one * 0.7f, 10);
            glowRing = Art.Make("readyRing", transform, Art.Ring, Palette.HeroGlow, new Vector2(0f, -0.4f), Vector2.one * 2.4f, 11);
            clock = Art.Make("rechargeClock", transform, Art.Radial(0f), new Color(1f, 0.82f, 0.35f, 0.75f), new Vector2(0f, 0.2f), Vector2.one * 0.8f, 12);
            clock.enabled = false;
        }

        public bool HitTest(Vector2 p) => (p - Position).sqrMagnitude <= 1.7f * 1.7f;

        /// <summary>Distance from a point to the boulder lane (for build blocking).</summary>
        public float DistanceToLane(Vector2 p) => PathRoute.DistanceToSegment(p, LaneFrom, LaneTo);

        public bool Trigger()
        {
            var lm = LevelManager.I;
            if (!Ready)
            {
                AudioManager.Play(Sfx.Error, 0.6f);
                if (lm != null) lm.AddPopup(Position + Vector2.down * 1.5f, DisplayName + " recharging: " + Mathf.CeilToInt(RechargeLeft) + "s", Palette.Dim);
                return false;
            }
            Current = Phase.Rumbling;
            timer = 0.7f;
            AudioManager.Play(Sfx.Rockslide);
            if (lm != null && lm.CamCtl != null) lm.CamCtl.Shake(0.7f, 0.12f);
            if (lm != null) lm.OnRockslideTriggered();
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            bool ready = Current == Phase.Ready;
            glowRing.enabled = ready;
            pile1.enabled = pile2.enabled = pile3.enabled = ready || Current == Phase.Rumbling;
            if (ready)
            {
                glowRing.color = new Color(1f, 0.82f, 0.4f, 0.4f + 0.4f * Mathf.Sin(t * 4f));
                glowRing.transform.localScale = Vector3.one * (2.3f + 0.15f * Mathf.Sin(t * 4f));
            }
            clock.enabled = Current == Phase.Recharging;
            if (clock.enabled) clock.sprite = Art.Radial(RechargeFrac);

            switch (Current)
            {
                case Phase.Rumbling:
                    timer -= dt;
                    Vector2 jit = Random.insideUnitCircle * 0.06f;
                    transform.position = basePos + new Vector3(jit.x, jit.y, 0f);
                    if (Random.value < 0.4f) Fx.Burst(Position + Random.insideUnitCircle * 1.2f, new Color(0.5f, 0.42f, 0.35f, 0.6f), 0.5f, 1.4f, 0.8f, Art.Circle, 13, new Vector3(0f, -0.6f, 0f));
                    if (timer <= 0f)
                    {
                        transform.position = basePos;
                        Current = Phase.Rolling;
                        boulders = 3;
                        timer = 0f;
                    }
                    break;
                case Phase.Rolling:
                    timer -= dt;
                    if (timer <= 0f && boulders > 0)
                    {
                        boulders--;
                        timer = 0.35f;
                        Vector2 jitter = new Vector2(Random.Range(-0.35f, 0.35f), 0f);
                        RockslideBoulder.Launch(LaneFrom + jitter, LaneTo + jitter, marsh);
                        if (boulders == 0)
                        {
                            Current = Phase.Recharging;
                            RechargeLeft = Balance.RockslideRecharge;
                        }
                    }
                    break;
                case Phase.Recharging:
                    RechargeLeft -= dt;
                    if (RechargeLeft <= 0f)
                    {
                        Current = Phase.Ready;
                        Fx.Ring(Position, Palette.HeroGlow, 0.5f, 2f, 0.5f);
                        if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.down * 1.5f, DisplayName + " ready!", Palette.HeroGlow);
                    }
                    break;
            }
        }
    }
}

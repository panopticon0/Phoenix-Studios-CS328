using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Core combat mechanic: towers auto-target the enemy furthest along the road, and every tower
    /// has a 3-branch x 3-tier upgrade tree (Power / Speed / Utility). A tower may take one branch to
    /// Tier 3 and a second branch to Tier 2, so two towers of the same type can play very differently.
    /// </summary>
    public class Tower : MonoBehaviour
    {
        public TowerType Type { get; private set; }
        public TowerDef Def { get; private set; }
        public BuildPlot Plot { get; private set; }
        public readonly int[] Levels = new int[3];
        public int Invested { get; private set; }

        // Computed stats
        public float Damage { get; private set; }
        public float Interval { get; private set; }
        public float Range { get; private set; }
        public float Splash { get; private set; }
        public DamageKind Kind { get; private set; }
        public bool Disabled => disabledTimer > 0f;
        public float DisabledLeft => disabledTimer;
        public bool SlowedByDrummer { get; private set; }
        public bool Overcharged => overchargeTimer > 0f;
        public Vector2 Position => transform.position;
        public int SellValue => Mathf.RoundToInt(Invested * Balance.SellRefund);

        // Upgrade effects
        bool pierce, markOnHit, dispelOnHit, arrowStorm, meteor, overcharge;
        float burnDps, slowMult = 1f, slowDuration, stunOnHit;
        int chain, shots = 1;

        float cooldown, disabledTimer, abilityTimer, overchargeTimer, t;
        Transform head;
        SpriteRenderer rangeRing, disabledMark1, disabledMark2, slowRing, glow;
        readonly List<SpriteRenderer> tint = new List<SpriteRenderer>();
        readonly List<Color> tintBase = new List<Color>();
        readonly SpriteRenderer[,] pips = new SpriteRenderer[3, 3];

        LevelManager LM => LevelManager.I;

        public static Tower Create(TowerType type, BuildPlot plot, Transform parent)
        {
            var go = new GameObject(TowerData.Get(type).Name);
            go.transform.SetParent(parent, false);
            go.transform.position = plot.transform.position;
            var tower = go.AddComponent<Tower>();
            tower.Type = type;
            tower.Def = TowerData.Get(type);
            tower.Plot = plot;
            tower.Invested = tower.Def.Cost;
            tower.BuildVisuals();
            tower.Recalculate();
            tower.cooldown = 0.3f;
            return tower;
        }

        SpriteRenderer Part(string name, Sprite s, Color c, Vector2 pos, Vector2 size, int order, float rot = 0f, Transform parent = null)
        {
            var sr = Art.Make(name, parent != null ? parent : transform, s, c, pos, size, order, rot);
            tint.Add(sr);
            tintBase.Add(c);
            return sr;
        }

        void BuildVisuals()
        {
            rangeRing = Art.Make("range", transform, Art.Ring, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, Vector2.one, 11);
            Art.Make("rangeFill", rangeRing.transform, Art.Circle, new Color(1f, 1f, 1f, 0.05f), Vector2.zero, Vector2.one, 10);
            rangeRing.enabled = false;
            rangeRing.transform.GetChild(0).gameObject.SetActive(false);

            slowRing = Art.Make("drummerSlow", transform, Art.Ring, new Color(0.85f, 0.3f, 0.2f, 0.9f), Vector2.zero, Vector2.one * 1.5f, 12);
            slowRing.enabled = false;

            Part("base", Art.Square, Palette.StoneDark, Vector2.zero, new Vector2(1.2f, 1.2f), 12);
            Part("wall", Art.Square, Palette.Stone, Vector2.zero, new Vector2(1.0f, 1.0f), 13);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.42f, y = (i < 2 ? -1f : 1f) * 0.42f;
                Part("merlon", Art.Square, Palette.StoneDark, new Vector2(x, y), new Vector2(0.22f, 0.22f), 14);
            }

            head = new GameObject("Head").transform;
            head.SetParent(transform, false);

            switch (Type)
            {
                case TowerType.Archer:
                    Part("roof", Art.Circle, Def.Roof, Vector2.zero, new Vector2(0.7f, 0.7f), 15);
                    Part("bow", Art.Circle, Palette.Hex("#C9A46A"), new Vector2(0.18f, 0f), new Vector2(0.18f, 0.62f), 16, 0f, head);
                    Part("arrow", Art.Square, Palette.Hex("#E8DCC0"), new Vector2(0.25f, 0f), new Vector2(0.5f, 0.05f), 17, 0f, head);
                    break;
                case TowerType.Catapult:
                    Part("frame", Art.Square, Def.Roof, Vector2.zero, new Vector2(0.72f, 0.5f), 15);
                    Part("arm", Art.Square, Palette.Hex("#5E3E22"), new Vector2(0.2f, 0f), new Vector2(0.75f, 0.12f), 16, 0f, head);
                    Part("bucket", Art.Circle, Palette.Hex("#6B6058"), new Vector2(0.55f, 0f), new Vector2(0.26f, 0.26f), 17, 0f, head);
                    break;
                case TowerType.Mage:
                    Part("roof", Art.Circle, Def.Roof, Vector2.zero, new Vector2(0.75f, 0.75f), 15);
                    glow = Art.Make("orbGlow", transform, Art.Glow, new Color(0.7f, 0.55f, 1f, 0.8f), Vector2.zero, Vector2.one * 1.1f, 16);
                    Part("orb", Art.Circle, Palette.Hex("#D8C8FF"), Vector2.zero, new Vector2(0.28f, 0.28f), 17);
                    break;
            }

            // Upgrade pips: three columns (Power, Speed, Utility) of three dots under the tower.
            for (int b = 0; b < 3; b++)
                for (int tier = 0; tier < 3; tier++)
                    pips[b, tier] = Art.Make("pip", transform, Art.Circle, new Color(0f, 0f, 0f, 0.6f), new Vector2(-0.28f + b * 0.28f, -0.74f - tier * 0.16f), Vector2.one * 0.13f, 18);

            disabledMark1 = Art.Make("ko1", transform, Art.Square, Palette.Bad, Vector2.zero, new Vector2(1.1f, 0.16f), 19, 45f);
            disabledMark2 = Art.Make("ko2", transform, Art.Square, Palette.Bad, Vector2.zero, new Vector2(1.1f, 0.16f), 19, -45f);
            disabledMark1.enabled = disabledMark2.enabled = false;
        }

        public void SetSelected(bool selected)
        {
            rangeRing.enabled = selected;
            rangeRing.transform.GetChild(0).gameObject.SetActive(selected);
        }

        /// <summary>Upgrade rule: one branch to Tier 3, a second branch to Tier 2, the third stays locked.</summary>
        public bool CanUpgrade(int branch, out string reason)
        {
            if (Levels[branch] >= 3) { reason = "Maxed"; return false; }
            var test = new int[] { Levels[0], Levels[1], Levels[2] };
            test[branch]++;
            System.Array.Sort(test);
            // ascending after sort: test[2] largest
            if (test[2] > 3 || test[1] > 2 || test[0] > 0)
            {
                reason = "Path locked";
                return false;
            }
            reason = "";
            return true;
        }

        public int NextCost(int branch) => TowerData.UpgradeCost(Type, Levels[branch] + 1);

        public void ApplyUpgrade(int branch, int cost)
        {
            Levels[branch]++;
            Invested += cost;
            Recalculate();
            Fx.Ring(Position, Palette.Branch(branch), 0.4f, 1.4f, 0.4f);
        }

        void Recalculate()
        {
            int p = Levels[0], s = Levels[1], u = Levels[2];
            float dmgMult = 1f, rangeMult = 1f;
            float speedBonus = s >= 2 ? 0.4f : s >= 1 ? 0.2f : 0f;
            Kind = Def.Kind;
            Splash = Def.Splash;
            pierce = markOnHit = dispelOnHit = arrowStorm = meteor = overcharge = false;
            burnDps = 0f; slowMult = 1f; slowDuration = 0f; stunOnHit = 0f; chain = 0; shots = 1;

            switch (Type)
            {
                case TowerType.Archer:
                    if (p >= 1) dmgMult += 0.2f;
                    if (p >= 2) pierce = true;
                    if (p >= 3) { dmgMult += 0.3f; Kind = DamageKind.True; }
                    if (s >= 3) arrowStorm = true;
                    if (u >= 1) rangeMult += 0.2f;
                    if (u >= 2) markOnHit = true;
                    if (u >= 3) burnDps = 6f;
                    break;
                case TowerType.Catapult:
                    if (p >= 1) dmgMult += 0.25f;
                    if (p >= 2) Splash *= 1.4f;
                    if (p >= 3) stunOnHit = 1f;
                    if (s >= 3) shots = 3;
                    if (u >= 1) { slowMult = 0.7f; slowDuration = 2f; }
                    if (u >= 2) burnDps = 8f;
                    if (u >= 3) rangeMult += 0.3f;
                    break;
                case TowerType.Mage:
                    if (p >= 1) dmgMult += 0.2f;
                    if (p >= 2) chain = 2;
                    if (p >= 3) meteor = true;
                    if (s >= 3) overcharge = true;
                    if (u >= 1) { slowMult = 0.75f; slowDuration = 2f; }
                    if (u >= 2) rangeMult += 0.2f;
                    if (u >= 3) dispelOnHit = true;
                    break;
            }

            Damage = Def.Damage * dmgMult;
            Interval = Def.Interval / (1f + speedBonus);
            Range = Def.Range * rangeMult;
            rangeRing.transform.localScale = Vector3.one * Range * 2f;

            for (int b = 0; b < 3; b++)
                for (int tier = 0; tier < 3; tier++)
                    pips[b, tier].color = Levels[b] > tier ? Palette.Branch(b) : new Color(0f, 0f, 0f, 0.6f);
        }

        /// <summary>Text lines for the HUD tower panel.</summary>
        public List<string> SpecialLines()
        {
            var lines = new List<string>();
            if (Type == TowerType.Catapult) lines.Add("Splash radius " + Splash.ToString("0.0"));
            if (pierce) lines.Add("Arrows pierce to a second enemy");
            if (Kind == DamageKind.True && Type == TowerType.Archer) lines.Add("Ignores armor");
            if (markOnHit) lines.Add("Marks targets (+15% damage taken)");
            if (burnDps > 0f) lines.Add("Burns for " + burnDps.ToString("0") + " dmg/s");
            if (slowDuration > 0f) lines.Add("Slows enemies " + Mathf.RoundToInt((1f - slowMult) * 100f) + "%");
            if (stunOnHit > 0f) lines.Add("Stuns for " + stunOnHit.ToString("0.#") + "s");
            if (chain > 0) lines.Add("Chains to " + chain + " more enemies");
            if (dispelOnHit) lines.Add("Dispels War Cry and Drummers");
            if (shots > 1) lines.Add("Fires " + shots + " stones per volley");
            if (arrowStorm) lines.Add("Arrow Storm every 8s");
            if (meteor) lines.Add("Meteor every 10s");
            if (overcharge) lines.Add("Overcharge every 12s");
            return lines;
        }

        public void Disable(float duration)
        {
            disabledTimer = Mathf.Max(disabledTimer, duration);
            Fx.Burst(Position, new Color(1f, 0.3f, 0.2f, 0.8f), 0.8f, 2f, 0.4f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || LM == null) return;
            t += dt;

            bool wasDisabled = disabledTimer > 0f;
            if (disabledTimer > 0f) disabledTimer -= dt;
            bool disabled = disabledTimer > 0f;
            disabledMark1.enabled = disabledMark2.enabled = disabled;
            if (disabled || wasDisabled) ApplyTint(disabled ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white);
            if (disabled) return;

            SlowedByDrummer = LM.IsTowerSlowed(Position);
            slowRing.enabled = SlowedByDrummer;
            if (SlowedByDrummer) slowRing.transform.localScale = Vector3.one * (1.45f + 0.08f * Mathf.Sin(t * 10f));

            float rate = SlowedByDrummer ? Balance.DrummerTowerSlow : 1f;
            if (overchargeTimer > 0f) { overchargeTimer -= dt; rate *= 2f; }
            if (glow != null) glow.transform.localScale = Vector3.one * ((Overcharged ? 1.6f : 1.1f) + 0.1f * Mathf.Sin(t * 5f));

            cooldown -= dt * rate;
            Enemy target = FindTarget(Range);
            if (target != null)
            {
                Aim(target.Position);
                if (cooldown <= 0f)
                {
                    Fire(target);
                    cooldown = Interval;
                }
            }
            else cooldown = Mathf.Max(cooldown, 0f);

            if (arrowStorm || meteor || overcharge)
            {
                abilityTimer -= dt * rate;
                if (abilityTimer <= 0f && target != null)
                {
                    if (arrowStorm) { ArrowStorm(); abilityTimer = 8f; }
                    else if (meteor) { Meteor(target); abilityTimer = 10f; }
                    else if (overcharge) { overchargeTimer = 4f; abilityTimer = 12f; Fx.Ring(Position, Palette.Magic, 0.5f, 1.6f, 0.5f); }
                }
            }
        }

        void ApplyTint(Color mul)
        {
            for (int i = 0; i < tint.Count; i++)
            {
                Color c = tintBase[i];
                tint[i].color = new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a);
            }
        }

        Enemy FindTarget(float range)
        {
            Enemy best = null;
            float bestDist = -1f;
            float r2 = range * range;
            foreach (var e in LM.Enemies)
            {
                if (e == null || !e.Alive) continue;
                if ((e.Position - Position).sqrMagnitude > r2) continue;
                if (e.Distance > bestDist) { bestDist = e.Distance; best = e; }
            }
            return best;
        }

        void Aim(Vector2 at)
        {
            Vector2 d = at - Position;
            if (d.sqrMagnitude > 0.001f) head.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        void Fire(Enemy target)
        {
            Vector3 from = transform.position + new Vector3(0f, 0.2f, 0f);
            switch (Type)
            {
                case TowerType.Archer:
                    ShootArrow(from, target, Damage);
                    break;
                case TowerType.Catapult:
                    for (int i = 0; i < shots; i++)
                    {
                        Vector2 aim = target.Position + (i == 0 ? Vector2.zero : Random.insideUnitCircle * 1.1f);
                        LobStone(from, aim, 1.0f + i * 0.12f);
                    }
                    break;
                case TowerType.Mage:
                    ShootBolt(from, target);
                    break;
            }
        }

        void ShootArrow(Vector3 from, Enemy target, float dmg)
        {
            var kind = Kind;
            Projectile.Homing(from, target, 16f, Art.Square, Palette.Hex("#E8DCC0"), new Vector2(0.42f, 0.07f), e =>
            {
                Hit(e, dmg, kind);
                if (pierce && LM != null)
                {
                    var second = LM.ClosestEnemy(e.Position, 1.6f, e);
                    if (second != null)
                    {
                        Fx.Line(e.Position, second.Position, new Color(1f, 0.95f, 0.8f, 0.6f), 0.06f, 0.15f);
                        Hit(second, dmg * 0.75f, kind);
                    }
                }
            });
        }

        void LobStone(Vector3 from, Vector2 aim, float flightTime)
        {
            float dmg = Damage, radius = Splash;
            var kind = Kind;
            Projectile.Lob(from, aim, flightTime, 2.2f, Art.Circle, Palette.Hex("#8A8078"), new Vector2(0.38f, 0.38f), 0f, land =>
            {
                Fx.Burst(land, new Color(0.75f, 0.6f, 0.45f, 0.8f), radius * 0.6f, radius * 2.2f, 0.35f);
                Fx.Ring(land, new Color(1f, 0.85f, 0.6f, 0.6f), 0.2f, radius, 0.3f);
                if (burnDps > 0f) Fx.Burst(land, new Color(1f, 0.5f, 0.1f, 0.6f), radius, radius * 1.8f, 0.6f);
                if (LM == null) return;
                foreach (var e in LM.EnemiesInRadius(land, radius))
                    Hit(e, dmg, kind);
            });
        }

        void ShootBolt(Vector3 from, Enemy target)
        {
            float dmg = Damage;
            Projectile.Homing(from, target, 11f, Art.Glow, new Color(0.75f, 0.6f, 1f, 1f), new Vector2(0.5f, 0.5f), e =>
            {
                Fx.Burst(e.Position, new Color(0.75f, 0.6f, 1f, 0.9f), 0.4f, 1.1f, 0.25f);
                var hitSoFar = new List<Enemy> { e };
                Vector2 lastPos = e.Position;
                Hit(e, dmg, DamageKind.Magic);
                if (chain > 0 && LM != null)
                {
                    for (int i = 0; i < chain; i++)
                    {
                        Enemy next = null;
                        float best = 2.5f * 2.5f;
                        foreach (var c in LM.Enemies)
                        {
                            if (c == null || !c.Alive || hitSoFar.Contains(c)) continue;
                            float d2 = (c.Position - lastPos).sqrMagnitude;
                            if (d2 < best) { best = d2; next = c; }
                        }
                        if (next == null) break;
                        Fx.Line(lastPos, next.Position, new Color(0.8f, 0.7f, 1f, 0.9f), 0.08f, 0.2f);
                        hitSoFar.Add(next);
                        lastPos = next.Position;
                        Hit(next, dmg * 0.6f, DamageKind.Magic);
                    }
                }
            });
        }

        void ArrowStorm()
        {
            var inRange = LM.EnemiesInRadius(Position, Range);
            if (inRange.Count == 0) return;
            Fx.Ring(Position, Palette.BranchSpeed, 0.5f, 1.8f, 0.4f);
            Vector3 from = transform.position + new Vector3(0f, 0.2f, 0f);
            for (int i = 0; i < 6; i++)
                ShootArrow(from, inRange[Random.Range(0, inRange.Count)], Damage * 0.7f);
        }

        void Meteor(Enemy target)
        {
            Vector2 aim = target.Position;
            Vector3 from = new Vector3(aim.x - 3f, aim.y + 6f, 0f);
            Fx.Ring(aim, new Color(1f, 0.4f, 0.2f, 0.7f), 1.8f, 0.4f, 0.8f);
            Projectile.Lob(from, aim, 0.8f, 0.5f, Art.Glow, new Color(1f, 0.55f, 0.2f, 1f), new Vector2(1.1f, 1.1f), 0f, land =>
            {
                Fx.Burst(land, new Color(1f, 0.6f, 0.2f, 0.9f), 1f, 4.2f, 0.5f);
                Fx.Ring(land, new Color(1f, 0.85f, 0.5f, 0.8f), 0.3f, 1.8f, 0.4f);
                if (LM == null) return;
                if (LM.CamCtl != null) LM.CamCtl.Shake(0.2f, 0.15f);
                foreach (var e in LM.EnemiesInRadius(land, 1.8f))
                    Hit(e, 150f, DamageKind.Magic);
            });
        }

        void Hit(Enemy e, float dmg, DamageKind kind)
        {
            if (e == null || !e.Alive) return;
            if (markOnHit) e.ApplyMark(4f);
            if (dispelOnHit) e.Dispel(3f);
            e.TakeDamage(dmg, kind);
            if (!e.Alive) return;
            if (slowDuration > 0f) e.ApplySlow(slowMult, slowDuration);
            if (burnDps > 0f) e.ApplyBurn(burnDps, 3f);
            if (stunOnHit > 0f) e.Stun(stunOnHit);
        }
    }
}

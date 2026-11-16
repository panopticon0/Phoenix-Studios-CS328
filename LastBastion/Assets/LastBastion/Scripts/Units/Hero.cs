using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// The player-controlled hero, driven by a <see cref="HeroDef"/>.
    ///  - Sir Aldric (melee): blocks up to 3 enemies and fights them. Q = Shield Slam (damage + stun around him).
    ///  - Kaela Swiftwind (ranged): doesn't block; shoots the enemy furthest down the road within range and reveals
    ///    invisible enemies in a wide radius. Q = Pinning Volley (damage + stun on a group she's aiming at).
    /// Right-click (or select, then left-click the ground) to move. Stuns INTERRUPT boss wind-ups.
    /// Heroes earn XP from every kill, level up to 10, and spend one hero point per level in a three-branch tree.
    /// </summary>
    public class Hero : MonoBehaviour
    {
        const float Regen = 14f;
        const float RespawnTime = 12f;
        const float RallyRadius = 5f;
        const float ContactRadius = 0.9f;     // ranged heroes get hit by enemies that walk into them
        const float ContactDamageMult = 0.5f;

        public HeroDef Def { get; private set; }
        public string Name => Def.ShortName;
        public bool Ranged => Def.Ranged;

        // Progression
        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int Points { get; private set; }
        public readonly int[] Tree = new int[3];

        bool Has(int branch, int tier) => Tree[branch] >= tier;

        public float MaxHp => Def.BaseHp * (!Ranged && Has(1, 1) ? 1.3f : 1f);
        public float Damage => Def.Damage * (1f + (Has(0, 1) ? 0.25f : 0f) + (Has(0, 3) ? 0.25f : 0f));
        public DamageKind Kind => Has(0, 3) ? DamageKind.True : DamageKind.Physical;
        public float AttackInterval => Def.AttackInterval * (Ranged && Has(1, 1) ? 0.8f : 1f);
        public float Range => Def.Range * (Ranged && Has(1, 2) ? 1.2f : 1f);
        public float Speed => Def.Speed * (Ranged && Has(1, 2) ? 1.3f : 1f);
        public float RevealRadius => Def.RevealRadius;
        // Aldric
        public bool Cleave => !Ranged && Has(0, 2);
        public int MaxBlock => Ranged ? 0 : (Has(1, 2) ? Def.MaxBlock + 1 : Def.MaxBlock);
        public float DamageTakenMult => !Ranged && Has(1, 2) ? 0.75f : 1f;
        public bool HasLastStand => !Ranged && Has(1, 3);
        public bool RallyCry => !Ranged && Has(2, 3);
        // Kaela
        public bool Piercing => Ranged && Has(0, 2);
        public bool TwinShot => Ranged && Has(1, 3);
        public bool HuntersMark => Ranged && Has(2, 3);
        // Ability (both)
        public float AbilityCooldown => Def.AbilityCooldown * (Has(2, 1) ? 0.75f : 1f);
        public float AbilityRadius => Def.AbilityRadius * (Has(2, 2) ? 1.4f : 1f);
        public float AbilityStun => Has(2, 2) ? Def.AbilityStun + 1f : Def.AbilityStun;
        public float AbilityDamage => Def.AbilityDamage;
        public float XpIntoLevel => Level >= HeroData.MaxLevel ? 1f : Mathf.InverseLerp(HeroData.LevelXp[Level - 1], HeroData.LevelXp[Level], Xp);
        public int XpToNext => Level >= HeroData.MaxLevel ? 0 : HeroData.LevelXp[Level] - Xp;

        public float Hp { get; private set; }
        public bool Dead => respawnTimer > 0f;
        public float RespawnLeft => respawnTimer;
        public float RespawnFrac => 1f - respawnTimer / RespawnTime;
        public float AbilityLeft => Mathf.Max(0f, abilityTimer);
        public bool AbilityReady => abilityTimer <= 0f && !Dead;
        public int BlockingCount => blocked.Count;
        public bool Moving => moving;
        public bool Invulnerable => invulnTimer > 0f;
        public Vector2 Position => transform.position;
        public Vector2 RespawnPoint => respawnPos;
        /// <summary>Ranged heroes: the enemy currently being shot at (null if none).</summary>
        public Enemy RangedTarget { get; private set; }

        readonly List<Enemy> blocked = new List<Enemy>();
        Vector2 respawnPos, moveTarget;
        bool moving, lastStandUsed;
        float attackTimer, regenDelay, respawnTimer, abilityTimer, flashTimer, invulnTimer, t;
        Transform visuals;
        SpriteRenderer bodyRenderer, selectRing, moveMarker, halo, heroRing, slamPreview, rangePreview, readyGlow, respawnClock, respawnBase;
        HealthBar bar;

        public static Hero Create(Transform parent, Vector2 start, Vector2 respawn, HeroDef def)
        {
            var go = new GameObject("Hero - " + def.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(start.x, start.y, 0f);
            var h = go.AddComponent<Hero>();
            h.Def = def;
            h.respawnPos = respawn;
            h.Level = 1;
            h.Hp = def.BaseHp;
            h.Build();
            return h;
        }

        void Build()
        {
            Color g = Def.Glow;
            // Bigger, brighter and always marked so players can find the hero at a glance.
            halo = Art.Make("halo", transform, Art.Glow, new Color(g.r, g.g, g.b, 0.55f), Vector2.zero, Vector2.one * 2.4f, 17);
            heroRing = Art.Make("heroRing", transform, Art.Ring, g, Vector2.zero, Vector2.one * 1.35f, 19);
            selectRing = Art.Make("select", transform, Art.Ring, Palette.Good, Vector2.zero, Vector2.one * 1.75f, 19);
            selectRing.enabled = false;
            slamPreview = Art.Make("abilityPreview", transform, Art.Ring, new Color(g.r, g.g, g.b, 0.35f), Vector2.zero, Vector2.one, 12);
            slamPreview.enabled = false;
            rangePreview = Art.Make("rangePreview", transform, Art.Ring, new Color(1f, 1f, 1f, 0.22f), Vector2.zero, Vector2.one, 12);
            rangePreview.enabled = false;
            Art.Make("shadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.06f, -0.17f), new Vector2(1.15f, 0.72f), 18);

            visuals = new GameObject("Visuals").transform;
            visuals.SetParent(transform, false);
            visuals.localScale = Vector3.one * 1.25f;
            if (Ranged)
            {
                Art.Make("cloak", visuals, Art.Circle, Def.Cloak, new Vector2(-0.14f, 0f), new Vector2(0.74f, 0.78f), 24);
                Art.Make("quiver", visuals, Art.Square, Palette.Hex("#7A5232"), new Vector2(-0.22f, -0.16f), new Vector2(0.36f, 0.12f), 25, 30f);
                bodyRenderer = Art.Make("body", visuals, Art.Circle, Def.Body, Vector2.zero, new Vector2(0.62f, 0.62f), 25);
                Art.Make("trim", visuals, Art.Ring, Def.Trim, Vector2.zero, new Vector2(0.66f, 0.66f), 26);
                Art.Make("hood", visuals, Art.Circle, Def.Cloak, new Vector2(0.02f, 0f), new Vector2(0.36f, 0.36f), 27);
                Art.Make("face", visuals, Art.Circle, Palette.Hex("#E9C9A4"), new Vector2(0.1f, 0f), new Vector2(0.18f, 0.2f), 28);
                Art.Make("bow", visuals, Art.Ring, Palette.Hex("#C8A46A"), new Vector2(0.36f, 0f), new Vector2(0.34f, 0.8f), 27);
                Art.Make("string", visuals, Art.Square, new Color(1f, 1f, 1f, 0.8f), new Vector2(0.36f, 0f), new Vector2(0.03f, 0.72f), 28);
            }
            else
            {
                Art.Make("cape", visuals, Art.Circle, Def.Cloak, new Vector2(-0.16f, 0f), new Vector2(0.72f, 0.82f), 24);
                bodyRenderer = Art.Make("body", visuals, Art.Circle, Def.Body, Vector2.zero, new Vector2(0.72f, 0.72f), 25);
                Art.Make("trim", visuals, Art.Ring, Def.Trim, Vector2.zero, new Vector2(0.76f, 0.76f), 26);
                Art.Make("helm", visuals, Art.Circle, Palette.Hex("#D8D2CA"), new Vector2(0.04f, 0f), new Vector2(0.36f, 0.36f), 27);
                Art.Make("plume", visuals, Art.Triangle, Palette.Hex("#E8C547"), new Vector2(-0.18f, 0f), new Vector2(0.18f, 0.34f), 28, 90f);
                Art.Make("shield", visuals, Art.Circle, Palette.Hex("#D9C27A"), new Vector2(0.14f, 0.33f), new Vector2(0.34f, 0.24f), 27);
                Art.Make("sword", visuals, Art.Square, Palette.Hex("#F0ECE6"), new Vector2(0.4f, -0.26f), new Vector2(0.6f, 0.08f), 27);
            }
            readyGlow = Art.Make("readyGlow", transform, Art.Ring, new Color(g.r, g.g, g.b, 0.9f), Vector2.zero, Vector2.one * 1.1f, 29);

            bar = new HealthBar(transform, 1.1f, 0.85f, Palette.Good);
            bar.AlwaysShow = true;
            bar.Set(1f);

            moveMarker = Art.Make("moveMarker", null, Art.Ring, Palette.WithAlpha(Palette.Good, 0.8f), Vector2.zero, Vector2.one * 0.7f, 6);
            moveMarker.enabled = false;

            // Respawn indicator at the gate: a clock that fills while the hero recovers.
            respawnBase = Art.Make("respawnBase", null, Art.Circle, new Color(0f, 0f, 0f, 0.55f), respawnPos, Vector2.one * 1.5f, 30);
            respawnClock = Art.Make("respawnClock", null, Art.Radial(0f), new Color(g.r, g.g, g.b, 0.85f), respawnPos, Vector2.one * 1.3f, 31);
            respawnBase.enabled = respawnClock.enabled = false;
        }

        void OnDestroy()
        {
            if (moveMarker != null) Destroy(moveMarker.gameObject);
            if (respawnBase != null) Destroy(respawnBase.gameObject);
            if (respawnClock != null) Destroy(respawnClock.gameObject);
        }

        public bool HitTest(Vector2 p) => !Dead && (p - Position).sqrMagnitude < 0.8f * 0.8f;

        public void SetSelected(bool selected)
        {
            if (selectRing != null) selectRing.enabled = selected && !Dead;
            if (slamPreview != null) slamPreview.enabled = selected && !Dead && !Ranged;
            if (rangePreview != null) rangePreview.enabled = selected && !Dead && Ranged;
        }

        public void MoveTo(Vector2 target)
        {
            if (Dead) return;
            moveTarget = target;
            moving = true;
            ReleaseAll();
            moveMarker.transform.position = new Vector3(target.x, target.y, 0f);
            moveMarker.enabled = true;
            Fx.Ring(target, Palette.Good, 0.6f, 0.15f, 0.3f);
        }

        // ------------------------------------------------------------------ Progression

        public void AddXp(int amount)
        {
            if (Level >= HeroData.MaxLevel || amount <= 0) return;
            Xp += amount;
            while (Level < HeroData.MaxLevel && Xp >= HeroData.LevelXp[Level])
            {
                Level++;
                Points++;
                Fx.Ring(Position, Def.Glow, 0.4f, 2.6f, 0.6f);
                Fx.Sparks(Position, Def.Glow, 18, 4f, 0.7f);
                AudioManager.Play(Sfx.LevelUp);
                var lm = LevelManager.I;
                if (lm != null)
                {
                    lm.AddPopup(Position + Vector2.up * 1.2f, "LEVEL " + Level + "!", Palette.HeroGlow);
                    lm.OnHeroLevelUp(this);
                }
            }
        }

        public bool CanLearn(int branch) => Points > 0 && Tree[branch] < 3;

        public bool Learn(int branch)
        {
            if (!CanLearn(branch)) return false;
            float hpFrac = Hp / MaxHp;
            Tree[branch]++;
            Points--;
            Hp = MaxHp * hpFrac;
            bar.Set(Hp / MaxHp);
            Fx.Ring(Position, Palette.Branch(branch), 0.4f, 1.8f, 0.4f);
            AudioManager.Play(Sfx.Upgrade);
            return true;
        }

        // ------------------------------------------------------------------ Combat

        /// <summary>Where the ability would land right now (ranged: the best target in range; melee: on the hero).</summary>
        public bool AbilityCenter(out Vector2 center)
        {
            center = Position;
            if (!Ranged) return true;
            var e = FindTarget(null);
            if (e == null) return false;
            center = e.Position;
            return true;
        }

        public bool UseAbility()
        {
            if (!AbilityReady) { if (!Dead) AudioManager.Play(Sfx.Error, 0.6f); return false; }
            var lm = LevelManager.I;
            if (!AbilityCenter(out Vector2 c))
            {
                AudioManager.Play(Sfx.Error, 0.6f);
                if (lm != null) lm.AddPopup(Position + Vector2.up * 1.2f, "No enemy in range", Palette.Muted);
                return false;
            }
            abilityTimer = AbilityCooldown;
            float r = AbilityRadius;
            if (Ranged)
            {
                Fx.Ring(c, Def.Glow, 0.4f, r, 0.35f);
                Fx.Burst(c, new Color(0.85f, 1f, 0.6f, 0.5f), 0.6f, r * 2f, 0.3f);
                for (int i = 0; i < 10; i++)
                {
                    Vector2 p = c + Random.insideUnitCircle * r;
                    Fx.Line(p + new Vector2(-0.5f, 1.6f), p, new Color(0.95f, 0.9f, 0.75f, 0.9f), 0.06f, 0.18f);
                }
                Fx.Sparks(c, new Color(0.9f, 0.85f, 0.6f, 0.9f), 14, r * 2f, 0.45f);
                AudioManager.Play(Sfx.Arrow); AudioManager.Play(Sfx.Impact, 0.7f);
            }
            else
            {
                Fx.Ring(c, Palette.Gold, 0.4f, r, 0.35f);
                Fx.Ring(c, new Color(1f, 1f, 1f, 0.6f), 0.2f, r * 0.7f, 0.25f);
                Fx.Burst(c, new Color(1f, 0.9f, 0.6f, 0.6f), 1f, r * 2f, 0.3f);
                Fx.Sparks(c, new Color(0.8f, 0.7f, 0.5f, 0.9f), 16, r * 2.5f, 0.5f);
                AudioManager.Play(Sfx.Slam);
            }
            if (lm != null)
            {
                foreach (var e in lm.EnemiesInRadius(c, r))
                {
                    e.Stun(AbilityStun);
                    if (HuntersMark) { e.ApplyMark(6f); e.Reveal(8f); }
                    e.TakeDamage(AbilityDamage, Kind, null, true);
                }
                if (RallyCry)
                {
                    foreach (var tower in lm.Towers)
                        if (tower != null && Vector2.Distance(tower.Position, Position) <= RallyRadius) tower.ApplyRally(6f);
                    Fx.Ring(Position, Palette.HeroGlow, 0.5f, RallyRadius, 0.6f);
                }
                if (lm.CamCtl != null) lm.CamCtl.Shake(0.2f, 0.15f);
                lm.OnHeroSlam();
            }
            return true;
        }

        public void TakeDamage(float amount)
        {
            if (Dead || amount <= 0f || invulnTimer > 0f) return;
            Hp -= amount * DamageTakenMult;
            regenDelay = 3f;
            if (amount >= 1f) flashTimer = 0.08f;
            if (Hp <= 0f && HasLastStand && !lastStandUsed)
            {
                lastStandUsed = true;
                Hp = MaxHp * 0.3f;
                invulnTimer = 3f;
                Fx.Ring(Position, Palette.HeroGlow, 0.3f, 2f, 0.5f);
                AudioManager.Play(Sfx.LevelUp, 0.7f);
                if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.2f, "LAST STAND!", Palette.HeroGlow);
            }
            bar.Set(Hp / MaxHp);
            if (Hp <= 0f) Die();
        }

        public void Release(Enemy e)
        {
            blocked.Remove(e);
            if (e != null && e.BlockedBy == this) e.BlockedBy = null;
        }

        void ReleaseAll()
        {
            foreach (var e in blocked)
                if (e != null && e.BlockedBy == this) e.BlockedBy = null;
            blocked.Clear();
        }

        void Die()
        {
            Hp = 0f;
            ReleaseAll();
            moving = false;
            RangedTarget = null;
            moveMarker.enabled = false;
            respawnTimer = RespawnTime;
            visuals.gameObject.SetActive(false);
            halo.enabled = heroRing.enabled = readyGlow.enabled = slamPreview.enabled = rangePreview.enabled = selectRing.enabled = false;
            bar.Show(false);
            respawnBase.enabled = respawnClock.enabled = true;
            Fx.Burst(Position, new Color(0.6f, 0.75f, 1f, 0.8f), 0.6f, 2.2f, 0.5f);
            AudioManager.Play(Sfx.HeroDown);
            if (LevelManager.I != null)
            {
                LevelManager.I.AddPopup(Position + Vector2.up * 0.8f, Name + " has fallen!", Palette.Bad);
                LevelManager.I.OnHeroDied();
            }
        }

        void Respawn()
        {
            respawnTimer = 0f;
            Hp = MaxHp;
            lastStandUsed = false;
            transform.position = new Vector3(respawnPos.x, respawnPos.y, 0f);
            visuals.gameObject.SetActive(true);
            halo.enabled = heroRing.enabled = true;
            bar.Show(true);
            bar.Set(1f);
            respawnBase.enabled = respawnClock.enabled = false;
            Fx.Ring(Position, Def.Glow, 0.3f, 1.5f, 0.5f);
            AudioManager.Play(Sfx.LevelUp, 0.5f);
            if (LevelManager.I != null)
            {
                LevelManager.I.AddPopup(Position + Vector2.up * 0.8f, Name + " returns!", Palette.Good);
                SetSelected(LevelManager.I.HeroSelected);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            if (abilityTimer > 0f) abilityTimer -= dt;
            if (invulnTimer > 0f) invulnTimer -= dt;

            if (Dead)
            {
                respawnTimer -= dt;
                respawnClock.sprite = Art.Radial(RespawnFrac);
                if (respawnTimer <= 0f) Respawn();
                return;
            }

            UpdateVisualCues();

            blocked.RemoveAll(e => e == null || !e.Alive || e.BlockedBy != this);
            var lm = LevelManager.I;

            if (moving)
            {
                Vector2 to = moveTarget - Position;
                float step = Speed * dt;
                if (to.magnitude <= step)
                {
                    transform.position = new Vector3(moveTarget.x, moveTarget.y, 0f);
                    moving = false;
                    moveMarker.enabled = false;
                }
                else
                {
                    Vector2 next = Position + to.normalized * step;
                    transform.position = new Vector3(next.x, next.y, 0f);
                    Face(to);
                }
            }

            bool fighting = Ranged ? UpdateRanged(lm, dt) : UpdateMelee(lm, dt);

            if (fighting) regenDelay = 3f;
            else
            {
                attackTimer = Mathf.Min(attackTimer, 0.2f);
                regenDelay -= dt;
                if (regenDelay <= 0f && Hp < MaxHp)
                {
                    Hp = Mathf.Min(MaxHp, Hp + Regen * dt);
                    bar.Set(Hp / MaxHp);
                }
            }

            if (flashTimer > 0f) flashTimer -= dt;
            Color baseC = invulnTimer > 0f ? Color.Lerp(Def.Body, Def.Glow, 0.5f + 0.5f * Mathf.Sin(t * 20f)) : Def.Body;
            bodyRenderer.color = flashTimer > 0f ? Color.Lerp(baseC, Color.white, 0.7f) : baseC;
            bar.SetColor(Hp / MaxHp > 0.35f ? Palette.Good : Palette.Bad);
        }

        bool UpdateMelee(LevelManager lm, float dt)
        {
            if (!moving && lm != null && blocked.Count < MaxBlock)
            {
                float r2 = Def.EngageRadius * Def.EngageRadius;
                foreach (var e in lm.Enemies)
                {
                    if (blocked.Count >= MaxBlock) break;
                    if (e == null || !e.Alive || e.BlockedBy != null || !e.CanBeBlocked) continue;
                    if ((e.Position - Position).sqrMagnitude <= r2)
                    {
                        e.BlockedBy = this;
                        blocked.Add(e);
                    }
                }
            }
            if (moving || blocked.Count == 0) return false;

            Enemy target = blocked[0];
            Face(target.Position - Position);
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = AttackInterval;
                var targets = Cleave ? new List<Enemy>(blocked) : new List<Enemy> { target };
                foreach (var e in targets)
                {
                    if (e == null || !e.Alive) continue;
                    Fx.Line(Position, e.Position, new Color(0.9f, 0.92f, 1f, 0.8f), 0.12f, 0.12f);
                    Fx.Burst(e.Position, new Color(1f, 1f, 1f, 0.7f), 0.3f, 0.8f, 0.18f);
                    e.TakeDamage(Damage, Kind, null, true);
                }
                AudioManager.Play(Sfx.Hit, 0.35f);
            }
            return true;
        }

        bool UpdateRanged(LevelManager lm, float dt)
        {
            if (lm == null) return false;
            bool hurt = false;
            // She doesn't block: enemies walking through her land glancing blows.
            float c2 = ContactRadius * ContactRadius;
            foreach (var e in lm.Enemies)
            {
                if (e == null || !e.Alive || e.Stunned) continue;
                if ((e.Position - Position).sqrMagnitude <= c2)
                {
                    TakeDamage(e.Def.HeroDps * ContactDamageMult * (1f + e.EnrageFrac) * dt);
                    hurt = true;
                    if (Dead) return false;
                }
            }

            RangedTarget = moving ? null : FindTarget(null);
            if (RangedTarget == null) return hurt;

            Face(RangedTarget.Position - Position);
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = AttackInterval;
                Shoot(RangedTarget);
                if (TwinShot)
                {
                    var second = FindTarget(RangedTarget);
                    Shoot(second != null ? second : RangedTarget);
                }
                AudioManager.Play(Sfx.Arrow, 0.4f);
            }
            return true;
        }

        /// <summary>The visible enemy furthest down the road within range.</summary>
        Enemy FindTarget(Enemy exclude)
        {
            var lm = LevelManager.I;
            if (lm == null) return null;
            Enemy best = null;
            float r2 = Range * Range;
            foreach (var e in lm.Enemies)
            {
                if (e == null || !e.Alive || e == exclude || e.IsHidden) continue;
                if ((e.Position - Position).sqrMagnitude > r2) continue;
                if (best == null || e.Distance > best.Distance) best = e;
            }
            return best;
        }

        void Shoot(Enemy target)
        {
            float dmg = Damage;
            var kind = Kind;
            bool pierce = Piercing;
            Projectile.Homing(transform.position, target, 18f, Art.Square, Def.Trim, new Vector2(0.44f, 0.07f), e =>
            {
                if (e == null || !e.Alive) return;
                Vector2 hitPos = e.Position;
                e.TakeDamage(dmg, kind, null, true);
                Fx.Burst(hitPos, new Color(1f, 1f, 0.85f, 0.6f), 0.2f, 0.6f, 0.15f);
                if (!pierce || LevelManager.I == null) return;
                Enemy next = null;
                float bestD = 1.6f * 1.6f;
                foreach (var o in LevelManager.I.Enemies)
                {
                    if (o == null || o == e || !o.Alive) continue;
                    float d = (o.Position - hitPos).sqrMagnitude;
                    if (d < bestD) { bestD = d; next = o; }
                }
                if (next != null)
                {
                    Fx.Line(hitPos, next.Position, new Color(1f, 1f, 0.85f, 0.7f), 0.05f, 0.12f);
                    next.TakeDamage(dmg * 0.6f, kind, null, true);
                }
            });
        }

        void UpdateVisualCues()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Color g = Def.Glow;
            halo.color = new Color(g.r, g.g, g.b, 0.35f + 0.25f * pulse);
            halo.transform.localScale = Vector3.one * (2.2f + 0.3f * pulse);
            heroRing.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            // Ability ready: bright pulsing ring. On cooldown: hidden.
            readyGlow.enabled = AbilityReady;
            if (readyGlow.enabled)
            {
                float k = (t * 1.2f) % 1f;
                readyGlow.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 2.1f, k);
                readyGlow.color = new Color(g.r, g.g, g.b, 0.8f * (1f - k));
            }
            if (slamPreview.enabled) slamPreview.transform.localScale = Vector3.one * AbilityRadius * 2f;
            if (rangePreview.enabled) rangePreview.transform.localScale = Vector3.one * Range * 2f;
        }

        void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            visuals.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }
}

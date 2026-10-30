using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// An orc walking the road toward the Bastion. Can be slowed, burned, marked, stunned,
    /// dispelled and blocked by the hero. The War Drummer's unique mechanic (tower-slowing aura)
    /// lives here too; the boss is a subclass.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public EnemyType Type { get; private set; }
        public EnemyDef Def { get; private set; }
        public float Hp { get; protected set; }
        public float MaxHp { get; protected set; }
        public float Distance { get; protected set; }
        public float SpeedBuff { get; private set; }
        public Hero BlockedBy;

        public bool Alive => !removed && Hp > 0f;
        public bool IsBoss => Type == EnemyType.Gorruk;
        public Vector2 Position => transform.position;
        public bool Stunned => stunTimer > 0f;
        public bool AuraActive => Type == EnemyType.WarDrummer && Alive && dispelTimer <= 0f;

        protected PathRoute path;
        protected HealthBar bar;
        protected Transform body;
        protected bool removed;

        float lane;
        float slowMult = 1f, slowTimer, burnDps, burnTimer, markTimer, stunTimer, dispelTimer, flashTimer, fxTimer, bob;
        SpriteRenderer bodyRenderer, aura, stunMark, frenzy;
        Color bodyColor;

        public static Enemy Create(EnemyType type, Transform parent, PathRoute route)
        {
            var go = new GameObject(EnemyData.Get(type).Name);
            go.transform.SetParent(parent, false);
            Enemy e = type == EnemyType.Gorruk ? go.AddComponent<BossGorruk>() : go.AddComponent<Enemy>();
            e.Setup(type, route);
            return e;
        }

        protected virtual void Setup(EnemyType type, PathRoute route)
        {
            Type = type;
            Def = EnemyData.Get(type);
            path = route;
            MaxHp = Hp = Def.MaxHp * GameSession.EnemyHealthMult;
            SpeedBuff = 1f;
            lane = IsBoss ? 0f : Random.Range(-0.28f, 0.28f);
            bob = Random.value * 10f;
            Distance = 0f;
            transform.position = PositionAt(0f);
            BuildVisuals();
            bar = new HealthBar(transform, IsBoss ? 1.8f : 0.8f, Def.Radius + 0.35f, Palette.Bad);
            bar.Set(1f);
        }

        void BuildVisuals()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            float r = Def.Radius;
            Art.Make("shadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.05f, -0.12f), new Vector2(r * 2.3f, r * 1.4f), 18);

            switch (Type)
            {
                case EnemyType.WolfRider:
                    bodyRenderer = Art.Make("wolf", body, Art.Circle, Def.Body, Vector2.zero, new Vector2(1.0f, 0.55f), 20);
                    Art.Make("snout", body, Art.Circle, Palette.Hex("#5E5852"), new Vector2(0.45f, 0f), new Vector2(0.38f, 0.32f), 21);
                    Art.Make("ear", body, Art.Triangle, Palette.Hex("#5E5852"), new Vector2(0.36f, 0.16f), new Vector2(0.14f, 0.18f), 21, -90f);
                    Art.Make("rider", body, Art.Circle, Palette.OrcArmor, new Vector2(-0.05f, 0f), new Vector2(0.42f, 0.42f), 22);
                    Art.Make("head", body, Art.Circle, Palette.OrcSkin, new Vector2(0f, 0f), new Vector2(0.24f, 0.24f), 23);
                    break;
                case EnemyType.WarDrummer:
                    aura = Art.Make("aura", transform, Art.Ring, new Color(0.85f, 0.3f, 0.2f, 0.35f), Vector2.zero, Vector2.one * Balance.DrummerAuraRadius * 2f, 7);
                    Art.Make("auraFill", aura.transform, Art.Circle, new Color(0.85f, 0.3f, 0.2f, 0.08f), Vector2.zero, Vector2.one, 6);
                    bodyRenderer = Art.Make("body", body, Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Art.Make("drum", body, Art.Circle, Palette.Gold, new Vector2(0.32f, 0f), new Vector2(0.46f, 0.46f), 22);
                    Art.Make("drumskin", body, Art.Circle, Palette.Hex("#E8D9B0"), new Vector2(0.32f, 0f), new Vector2(0.3f, 0.3f), 23);
                    Art.Make("head", body, Art.Circle, Palette.OrcSkin, new Vector2(-0.08f, 0f), new Vector2(0.36f, 0.36f), 21);
                    break;
                case EnemyType.Gorruk:
                    bodyRenderer = Art.Make("body", body, Art.Circle, Def.Body, Vector2.zero, new Vector2(1.6f, 1.6f), 20);
                    Art.Make("pauldronL", body, Art.Circle, Palette.Iron, new Vector2(0f, 0.62f), new Vector2(0.62f, 0.62f), 21);
                    Art.Make("pauldronR", body, Art.Circle, Palette.Iron, new Vector2(0f, -0.62f), new Vector2(0.62f, 0.62f), 21);
                    Art.Make("head", body, Art.Circle, Palette.OrcSkin, new Vector2(0.15f, 0f), new Vector2(0.7f, 0.7f), 22);
                    Art.Make("helm", body, Art.Circle, Palette.Hex("#4A4542"), new Vector2(0.05f, 0f), new Vector2(0.55f, 0.6f), 23);
                    Art.Make("hornL", body, Art.Triangle, Palette.Hex("#E8D9B0"), new Vector2(0.05f, 0.42f), new Vector2(0.22f, 0.4f), 24, 20f);
                    Art.Make("hornR", body, Art.Triangle, Palette.Hex("#E8D9B0"), new Vector2(0.05f, -0.42f), new Vector2(0.22f, 0.4f), 24, 160f);
                    Art.Make("axeHandle", body, Art.Square, Palette.Hex("#5A3A22"), new Vector2(0.55f, -0.75f), new Vector2(0.9f, 0.1f), 24);
                    Art.Make("axeBlade", body, Art.Circle, Palette.Hex("#C8C2BA"), new Vector2(0.95f, -0.75f), new Vector2(0.45f, 0.6f), 25);
                    break;
                default:
                    bodyRenderer = Art.Make("body", body, Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Art.Make("head", body, Art.Circle, Palette.OrcSkin, new Vector2(0.05f, 0f), new Vector2(0.34f, 0.34f), 21);
                    Art.Make("blade", body, Art.Square, Palette.Hex("#B8B2AA"), new Vector2(0.22f, -0.3f), new Vector2(0.45f, 0.08f), 22);
                    break;
            }
            bodyColor = bodyRenderer.color;

            stunMark = Art.Make("stun", transform, Art.Ring, Palette.Gold, new Vector2(0f, r + 0.15f), new Vector2(0.45f, 0.2f), 52);
            stunMark.enabled = false;
            frenzy = Art.Make("frenzy", transform, Art.Glow, new Color(1f, 0.25f, 0.1f, 0.55f), Vector2.zero, Vector2.one * r * 3.4f, 17);
            frenzy.enabled = false;
        }

        protected Vector2 PositionAt(float d)
        {
            Vector2 p = path.PositionAt(d);
            Vector2 dir = path.DirectionAt(d);
            return p + new Vector2(-dir.y, dir.x) * lane;
        }

        protected virtual void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            TickEffects(dt);
            if (!Alive) return;

            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                return;
            }

            if (BlockedBy != null)
            {
                if (BlockedBy.Dead) BlockedBy = null;
                else
                {
                    BlockedBy.TakeDamage(Def.HeroDps * dt);
                    Face(BlockedBy.Position - Position);
                    return;
                }
            }

            Move(dt);
        }

        protected void Move(float dt)
        {
            float speed = Def.Speed * slowMult * SpeedBuff;
            Distance += speed * dt;
            transform.position = PositionAt(Distance);
            Face(path.DirectionAt(Distance));
            if (body != null) body.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin((Time.time + bob) * speed * 6f));
            if (Distance >= path.Length) ReachGate();
        }

        void TickEffects(float dt)
        {
            if (slowTimer > 0f) { slowTimer -= dt; if (slowTimer <= 0f) slowMult = 1f; }
            if (markTimer > 0f) markTimer -= dt;
            if (dispelTimer > 0f) dispelTimer -= dt;
            if (burnTimer > 0f)
            {
                burnTimer -= dt;
                fxTimer -= dt;
                if (fxTimer <= 0f)
                {
                    fxTimer = 0.15f;
                    Fx.Burst(Position + Random.insideUnitCircle * Def.Radius, new Color(1f, 0.55f, 0.15f, 0.8f), 0.25f, 0.05f, 0.5f, null, 46, new Vector3(0f, 0.8f, 0f));
                }
                TakeDamage(burnDps * dt, DamageKind.True);
                if (!Alive) return;
            }

            if (flashTimer > 0f) flashTimer -= dt;
            Color c = bodyColor;
            if (slowTimer > 0f) c = Color.Lerp(c, Palette.Frost, 0.5f);
            if (markTimer > 0f) c = Color.Lerp(c, Palette.Gold, 0.25f);
            if (flashTimer > 0f) c = Color.Lerp(c, Color.white, 0.7f);
            bodyRenderer.color = c;

            stunMark.enabled = stunTimer > 0f;
            if (stunMark.enabled) stunMark.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 360f);
            frenzy.enabled = SpeedBuff > 1.01f;
            if (aura != null)
            {
                aura.enabled = AuraActive;
                aura.transform.localScale = Vector3.one * Balance.DrummerAuraRadius * 2f * (1f + 0.03f * Mathf.Sin(Time.time * 8f));
            }
        }

        protected void Face(Vector2 dir)
        {
            if (body == null || dir.sqrMagnitude < 0.0001f) return;
            body.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        public virtual void TakeDamage(float amount, DamageKind kind)
        {
            if (!Alive || amount <= 0f) return;
            if (kind == DamageKind.Physical) amount *= 1f - Def.Armor;
            if (markTimer > 0f) amount *= 1.15f;
            Hp -= amount;
            if (amount >= 1f) flashTimer = 0.08f;
            bar.Set(Hp / MaxHp);
            if (Hp <= 0f) Die();
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (!Alive) return;
            slowMult = Mathf.Min(slowTimer > 0f ? slowMult : 1f, multiplier);
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        public void ApplyBurn(float dps, float duration)
        {
            if (!Alive) return;
            burnDps = Mathf.Max(burnTimer > 0f ? burnDps : 0f, dps);
            burnTimer = Mathf.Max(burnTimer, duration);
        }

        public void ApplyMark(float duration) { if (Alive) markTimer = Mathf.Max(markTimer, duration); }

        public void Stun(float duration)
        {
            if (!Alive) return;
            if (IsBoss) duration *= 0.5f;
            stunTimer = Mathf.Max(stunTimer, duration);
        }

        /// <summary>Strip War Cry frenzy and silence War Drummer aura.</summary>
        public void Dispel(float duration)
        {
            if (!Alive) return;
            if (SpeedBuff > 1f) Fx.Ring(Position, Palette.Magic, 0.3f, 1.0f, 0.4f);
            SpeedBuff = 1f;
            dispelTimer = Mathf.Max(dispelTimer, duration);
        }

        public void ApplyWarCry()
        {
            if (!Alive) return;
            SpeedBuff = Balance.WarCrySpeed;
        }

        protected virtual void Die()
        {
            if (removed) return;
            removed = true;
            Hp = 0f;
            if (BlockedBy != null) BlockedBy.Release(this);
            Fx.Burst(Position, new Color(0.9f, 0.4f, 0.2f, 0.9f), Def.Radius * 1.5f, Def.Radius * 4f, 0.35f);
            Fx.Burst(Position, new Color(0.3f, 0.25f, 0.2f, 0.7f), Def.Radius * 2f, Def.Radius * 3f, 0.6f, Art.Circle, 16);
            if (LevelManager.I != null) LevelManager.I.EnemyKilled(this);
            Destroy(gameObject);
        }

        void ReachGate()
        {
            if (removed) return;
            removed = true;
            if (BlockedBy != null) BlockedBy.Release(this);
            if (LevelManager.I != null) LevelManager.I.EnemyReachedGate(this);
            Destroy(gameObject);
        }

        /// <summary>Remove without rewards (debug skip / restart).</summary>
        public void Despawn()
        {
            removed = true;
            if (BlockedBy != null) BlockedBy.Release(this);
            Destroy(gameObject);
        }
    }
}

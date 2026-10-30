using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Sir Aldric the Unbroken, the player-controlled hero.
    /// Right-click (or select him, then left-click the ground) to move. He blocks up to 3 enemies
    /// and fights them in melee. Q = Shield Slam (damage + 2s stun around him). Respawns at the gate.
    /// </summary>
    public class Hero : MonoBehaviour
    {
        public const string HeroName = "Sir Aldric the Unbroken";
        public const float MaxHp = 420f;
        const float Speed = 3.6f;
        const float Damage = 20f;
        const float AttackInterval = 0.75f;
        const float EngageRadius = 1.35f;
        const int MaxBlock = 3;
        const float Regen = 14f;
        const float RespawnTime = 12f;
        public const float AbilityCooldown = 14f;
        const float SlamRadius = 2.6f;
        const float SlamDamage = 45f;
        const float SlamStun = 2f;

        public float Hp { get; private set; }
        public bool Dead => respawnTimer > 0f;
        public float RespawnLeft => respawnTimer;
        public float AbilityLeft => Mathf.Max(0f, abilityTimer);
        public bool AbilityReady => abilityTimer <= 0f && !Dead;
        public int BlockingCount => blocked.Count;
        public bool Moving => moving;
        public Vector2 Position => transform.position;

        readonly List<Enemy> blocked = new List<Enemy>();
        Vector2 respawnPos, moveTarget;
        bool moving;
        float attackTimer, regenDelay, respawnTimer, abilityTimer, flashTimer;
        Transform visuals;
        SpriteRenderer bodyRenderer, selectRing, moveMarker;
        HealthBar bar;

        public static Hero Create(Transform parent, Vector2 start, Vector2 respawn)
        {
            var go = new GameObject("Hero - " + HeroName);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(start.x, start.y, 0f);
            var h = go.AddComponent<Hero>();
            h.respawnPos = respawn;
            h.Hp = MaxHp;
            h.Build();
            return h;
        }

        void Build()
        {
            selectRing = Art.Make("select", transform, Art.Ring, Palette.Good, Vector2.zero, Vector2.one * 1.5f, 19);
            selectRing.enabled = false;
            Art.Make("shadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.05f, -0.14f), new Vector2(0.95f, 0.6f), 18);

            visuals = new GameObject("Visuals").transform;
            visuals.SetParent(transform, false);
            Art.Make("cape", visuals, Art.Circle, Palette.Hex("#8E2A1E"), new Vector2(-0.16f, 0f), new Vector2(0.7f, 0.8f), 24);
            bodyRenderer = Art.Make("body", visuals, Art.Circle, Palette.HeroBlue, Vector2.zero, new Vector2(0.72f, 0.72f), 25);
            Art.Make("trim", visuals, Art.Ring, Palette.Gold, Vector2.zero, new Vector2(0.74f, 0.74f), 26);
            Art.Make("helm", visuals, Art.Circle, Palette.Hex("#C8C2BA"), new Vector2(0.04f, 0f), new Vector2(0.36f, 0.36f), 27);
            Art.Make("shield", visuals, Art.Circle, Palette.Hex("#D9C27A"), new Vector2(0.14f, 0.33f), new Vector2(0.34f, 0.24f), 27);
            Art.Make("sword", visuals, Art.Square, Palette.Hex("#E6E2DC"), new Vector2(0.38f, -0.26f), new Vector2(0.55f, 0.07f), 27);

            bar = new HealthBar(transform, 0.9f, 0.62f, Palette.Good);
            bar.AlwaysShow = true;
            bar.Set(1f);

            moveMarker = Art.Make("moveMarker", null, Art.Ring, Palette.WithAlpha(Palette.Good, 0.8f), Vector2.zero, Vector2.one * 0.7f, 6);
            moveMarker.enabled = false;
        }

        void OnDestroy()
        {
            if (moveMarker != null) Destroy(moveMarker.gameObject);
        }

        public bool HitTest(Vector2 p) => !Dead && (p - Position).sqrMagnitude < 0.65f * 0.65f;

        public void SetSelected(bool selected)
        {
            if (selectRing != null) selectRing.enabled = selected && !Dead;
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

        public bool UseAbility()
        {
            if (!AbilityReady) return false;
            abilityTimer = AbilityCooldown;
            Fx.Ring(Position, Palette.Gold, 0.4f, SlamRadius, 0.35f);
            Fx.Burst(Position, new Color(1f, 0.9f, 0.6f, 0.6f), 1f, SlamRadius * 2f, 0.3f);
            var lm = LevelManager.I;
            if (lm != null)
            {
                foreach (var e in lm.EnemiesInRadius(Position, SlamRadius))
                {
                    e.Stun(SlamStun);
                    e.TakeDamage(SlamDamage, DamageKind.Physical);
                }
                if (lm.CamCtl != null) lm.CamCtl.Shake(0.2f, 0.15f);
            }
            return true;
        }

        public void TakeDamage(float amount)
        {
            if (Dead || amount <= 0f) return;
            Hp -= amount;
            regenDelay = 3f;
            if (amount >= 1f) flashTimer = 0.08f;
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
            moveMarker.enabled = false;
            respawnTimer = RespawnTime;
            visuals.gameObject.SetActive(false);
            bar.Show(false);
            selectRing.enabled = false;
            Fx.Burst(Position, new Color(0.6f, 0.75f, 1f, 0.8f), 0.6f, 2.2f, 0.5f);
            if (LevelManager.I != null)
            {
                LevelManager.I.AddPopup(Position + Vector2.up * 0.8f, "Sir Aldric has fallen!", Palette.Bad);
                LevelManager.I.OnHeroDied();
            }
        }

        void Respawn()
        {
            respawnTimer = 0f;
            Hp = MaxHp;
            transform.position = new Vector3(respawnPos.x, respawnPos.y, 0f);
            visuals.gameObject.SetActive(true);
            bar.Show(true);
            bar.Set(1f);
            Fx.Ring(Position, Palette.Gold, 0.3f, 1.5f, 0.5f);
            if (LevelManager.I != null)
            {
                LevelManager.I.AddPopup(Position + Vector2.up * 0.8f, "Sir Aldric returns!", Palette.Good);
                SetSelected(LevelManager.I.HeroSelected);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (abilityTimer > 0f) abilityTimer -= dt;

            if (Dead)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f) Respawn();
                return;
            }

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
            else if (lm != null && blocked.Count < MaxBlock)
            {
                float r2 = EngageRadius * EngageRadius;
                foreach (var e in lm.Enemies)
                {
                    if (blocked.Count >= MaxBlock) break;
                    if (e == null || !e.Alive || e.BlockedBy != null) continue;
                    if ((e.Position - Position).sqrMagnitude <= r2)
                    {
                        e.BlockedBy = this;
                        blocked.Add(e);
                    }
                }
            }

            if (!moving && blocked.Count > 0)
            {
                Enemy target = blocked[0];
                Face(target.Position - Position);
                attackTimer -= dt;
                if (attackTimer <= 0f)
                {
                    attackTimer = AttackInterval;
                    Fx.Line(Position, target.Position, new Color(0.9f, 0.92f, 1f, 0.8f), 0.1f, 0.12f);
                    Fx.Burst(target.Position, new Color(1f, 1f, 1f, 0.7f), 0.3f, 0.8f, 0.18f);
                    target.TakeDamage(Damage, DamageKind.Physical);
                }
                regenDelay = 3f;
            }
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
            bodyRenderer.color = flashTimer > 0f ? Color.Lerp(Palette.HeroBlue, Color.white, 0.7f) : Palette.HeroBlue;
            bar.SetColor(Hp / MaxHp > 0.35f ? Palette.Good : Palette.Bad);
        }

        void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            visuals.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }
}

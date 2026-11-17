using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// An orc walking the road toward the Bastion. Handles slows, burns, marks, stuns, dispels,
    /// hero blocking, and the unique mechanics: War Drummer aura, Goblin Sneak invisibility,
    /// Berserker enrage and Troll regeneration.
    ///
    /// New combat mechanic (beta): INTERRUPTS. Bosses wind up their big abilities with a visible
    /// telegraph. Any stun during a wind-up cancels the ability and Staggers the enemy
    /// (+25% damage taken for 3s).
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public EnemyType Type { get; private set; }
        public EnemyDef Def { get; private set; }
        public float Hp { get; protected set; }
        public float MaxHp { get; protected set; }
        public float Distance { get; protected set; }
        public float SpeedBuff { get; private set; }
        public int SpawnWave { get; private set; }
        /// <summary>Raised or split off mid-fight (Necromancer skeletons, Golem shards): worth 1 gold, no shards.</summary>
        public bool Summoned { get; private set; }
        public Hero BlockedBy;

        /// <summary>The tower that hit this enemy last (for kill counts).</summary>
        public Tower LastTowerHit { get; private set; }
        public bool LastHitByHero { get; private set; }

        public bool Alive => !removed && Hp > 0f;
        public bool IsBoss => Def != null && Def.IsBoss;
        public Vector2 Position => transform.position;
        public bool Stunned => stunTimer > 0f;
        public bool Staggered => staggerTimer > 0f;
        public bool AuraActive => Type == EnemyType.WarDrummer && Alive && dispelTimer <= 0f;
        /// <summary>Goblin Sneak currently unseen: towers without sight can't target it.</summary>
        public bool IsHidden { get; private set; }
        public float EnrageFrac => Def.Berserk && MaxHp > 0f ? Mathf.Clamp01(1f - Hp / MaxHp) : 0f;

        // Interruptible abilities (bosses override these)
        public virtual bool WindingUp => false;
        public virtual string WindUpName => "";
        public virtual float WindUpProgress => 0f;
        protected virtual bool CanMove => true;
        protected virtual float SpeedMultiplier => 1f;
        protected virtual bool IgnoresBlock => false;
        /// <summary>False while charging (Death Knight): Sir Aldric can't hold him.</summary>
        public bool CanBeBlocked => !IgnoresBlock;
        protected virtual void OnInterrupted() { }
        /// <summary>Bosses like Vesperine can make themselves invisible.</summary>
        protected bool forceHidden;

        protected PathRoute path;
        protected HealthBar bar;
        protected Transform body;
        protected bool removed;

        float lane;
        float slowMult = 1f, slowTimer, burnDps, burnTimer, markTimer, stunTimer, dispelTimer, flashTimer, fxTimer, bob;
        float staggerTimer, revealTimer, sinceHit = 99f, regenFx, summonTimer;
        SpriteRenderer bodyRenderer, aura, stunMark, frenzy, staggerMark;
        readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        readonly List<Color> partColors = new List<Color>();
        Color bodyColor;
        Sprite artS, artN, artE;
        bool usesArt;
        SpriteRenderer shadow;

        public static Enemy Create(EnemyType type, Transform parent, PathRoute route, int wave)
        {
            var go = new GameObject(EnemyData.Get(type).Name);
            go.transform.SetParent(parent, false);
            Enemy e;
            switch (type)
            {
                case EnemyType.Gorruk: e = go.AddComponent<BossGorruk>(); break;
                case EnemyType.Brakka: e = go.AddComponent<BossBrakka>(); break;
                case EnemyType.Banshee: e = go.AddComponent<EnemyBanshee>(); break;
                case EnemyType.Necromancer: e = go.AddComponent<EnemyNecromancer>(); break;
                case EnemyType.DeathKnight: e = go.AddComponent<BossDeathKnight>(); break;
                case EnemyType.Vesperine: e = go.AddComponent<BossVesperine>(); break;
                default: e = go.AddComponent<Enemy>(); break;
            }
            e.Setup(type, route, wave);
            return e;
        }

        protected virtual void Setup(EnemyType type, PathRoute route, int wave)
        {
            Type = type;
            Def = EnemyData.Get(type);
            path = route;
            SpawnWave = wave;
            float scale = Def.IsBoss ? 1f : Balance.WaveHealthScale(wave);
            MaxHp = Hp = Def.MaxHp * GameSession.EnemyHealthMult * scale;
            SpeedBuff = 1f;
            lane = IsBoss ? 0f : Random.Range(-0.28f, 0.28f);
            bob = Random.value * 10f;
            Distance = 0f;
            transform.position = PositionAt(0f);
            BuildVisuals();
            ApplyArt();
            bar = new HealthBar(transform, IsBoss ? 1.8f : 0.8f, Def.Radius + 0.35f, Palette.Bad);
            if (IsBoss) bar.AlwaysShow = true;
            bar.Set(1f);
            summonTimer = Def.SummonInterval;
        }

        // If real art exists for this enemy, it replaces the code-drawn body circle.
        // If real art exists for this enemy, it replaces the code-drawn body.
        void ApplyArt()
        {
            string folder = Def.IsBoss ? "Bosses/" : "Enemies/";
            string artPath = folder + Type.ToString().ToLower();
            var art = Art.Load(artPath);
            //UnityEngine.Debug.Log("ApplyArt: " + artPath + " | found=" + (art != null));

            if (bodyRenderer == null || art == null) return;

            artS = art;                          // south-facing (default)
            artN = Art.Load(artPath + "_n");     // optional north-facing
            artE = Art.Load(artPath + "_e");     // optional east-facing (west = mirrored)
            usesArt = true;

            bodyRenderer.sprite = art;
            bodyRenderer.color = Color.white;
            bodyColor = Color.white;             // TickEffects tints from this every frame

            // Hide the code-drawn extras (helmet, spear, crown...) but keep the shadow, status
            // marks and frenzy glow, which aren't in `parts`. Boss auras stay.
            foreach (var sr in parts)
                if (sr != bodyRenderer && !(Def.IsBoss && sr.name == "aura")) sr.enabled = false;

            body.rotation = Quaternion.identity;

            // The old shadow was a blob offset behind a rotating circle. With upright art it should
            // be a small flat ellipse at the feet. Tweak the three numbers to taste.

            if (shadow != null) shadow.enabled = false;

            // Slightly reduce the Bone Golem's visual size.
            if (Type == EnemyType.BoneGolem)
            {
                body.localScale *= 0.85f;
            }
            // Slightly reduce the Necromancers visual size.
            if (Type == EnemyType.Necromancer)
            {
                body.localScale *= 0.85f;
            }
        }

        /// Place a freshly created enemy part-way along the road (summons, splits).
        public void PlaceAt(float distance, bool summoned)
        {
            Distance = Mathf.Clamp(distance, 0f, path.Length - 0.1f);
            Summoned = summoned;
            transform.position = PositionAt(Distance);
        }

        SpriteRenderer Part(string name, Sprite s, Color c, Vector2 pos, Vector2 size, int order, float rot = 0f)
        {
            var sr = Art.Make(name, body, s, c, pos, size, order, rot);
            parts.Add(sr);
            partColors.Add(c);
            return sr;
        }

        void BuildVisuals()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            float r = Def.Radius;
            shadow = Art.Make("shadow", transform, Art.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.05f, -0.12f), new Vector2(r * 2.3f, r * 1.4f), 18);

            switch (Type)
            {
                case EnemyType.WolfRider:
                    bodyRenderer = Part("wolf", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.0f, 0.55f), 20);
                    Part("snout", Art.Circle, Palette.Hex("#5E5852"), new Vector2(0.45f, 0f), new Vector2(0.38f, 0.32f), 21);
                    Part("ear", Art.Triangle, Palette.Hex("#5E5852"), new Vector2(0.36f, 0.16f), new Vector2(0.14f, 0.18f), 21, -90f);
                    Part("rider", Art.Circle, Palette.OrcArmor, new Vector2(-0.05f, 0f), new Vector2(0.42f, 0.42f), 22);
                    Part("head", Art.Circle, Palette.OrcSkin, Vector2.zero, new Vector2(0.24f, 0.24f), 23);
                    break;
                case EnemyType.WarDrummer:
                    aura = Art.Make("aura", transform, Art.Ring, new Color(0.85f, 0.3f, 0.2f, 0.35f), Vector2.zero, Vector2.one * Balance.DrummerAuraRadius * 2f, 7);
                    Art.Make("auraFill", aura.transform, Art.Circle, new Color(0.85f, 0.3f, 0.2f, 0.08f), Vector2.zero, Vector2.one, 6);
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("drum", Art.Circle, Palette.Gold, new Vector2(0.32f, 0f), new Vector2(0.46f, 0.46f), 22);
                    Part("drumskin", Art.Circle, Palette.Hex("#E8D9B0"), new Vector2(0.32f, 0f), new Vector2(0.3f, 0.3f), 23);
                    Part("head", Art.Circle, Palette.OrcSkin, new Vector2(-0.08f, 0f), new Vector2(0.36f, 0.36f), 21);
                    break;
                case EnemyType.Ironhide:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("plateL", Art.Square, Palette.Hex("#8C8680"), new Vector2(0f, 0.28f), new Vector2(0.5f, 0.18f), 21);
                    Part("plateR", Art.Square, Palette.Hex("#8C8680"), new Vector2(0f, -0.28f), new Vector2(0.5f, 0.18f), 21);
                    Part("helm", Art.Circle, Palette.Hex("#77716B"), new Vector2(0.05f, 0f), new Vector2(0.4f, 0.4f), 22);
                    Part("visor", Art.Square, Palette.Hex("#2A2622"), new Vector2(0.17f, 0f), new Vector2(0.08f, 0.26f), 23);
                    Part("shield", Art.Square, Palette.Hex("#6A6560"), new Vector2(0.33f, 0.18f), new Vector2(0.12f, 0.44f), 23);
                    break;
                case EnemyType.GoblinSneak:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("hood", Art.Circle, Palette.Hex("#2E3A22"), new Vector2(-0.04f, 0f), new Vector2(0.4f, 0.4f), 21);
                    Part("earL", Art.Triangle, Def.Body, new Vector2(0f, 0.24f), new Vector2(0.12f, 0.24f), 21, 0f);
                    Part("earR", Art.Triangle, Def.Body, new Vector2(0f, -0.24f), new Vector2(0.12f, 0.24f), 21, 180f);
                    Part("dagger", Art.Square, Palette.Hex("#D8D2CA"), new Vector2(0.28f, -0.12f), new Vector2(0.3f, 0.05f), 22);
                    break;
                case EnemyType.Berserker:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("head", Art.Circle, Palette.OrcSkin, new Vector2(0.06f, 0f), new Vector2(0.36f, 0.36f), 21);
                    Part("warpaint", Art.Square, Palette.Hex("#E8D9B0"), new Vector2(0.1f, 0f), new Vector2(0.06f, 0.3f), 22);
                    Part("axeL", Art.Triangle, Palette.Hex("#C8C2BA"), new Vector2(0.3f, 0.36f), new Vector2(0.28f, 0.22f), 22, -90f);
                    Part("axeR", Art.Triangle, Palette.Hex("#C8C2BA"), new Vector2(0.3f, -0.36f), new Vector2(0.28f, 0.22f), 22, -90f);
                    break;
                case EnemyType.Brakka:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.4f, 1.4f), 20);
                    Part("pauldronL", Art.Circle, Palette.Iron, new Vector2(0f, 0.55f), new Vector2(0.55f, 0.55f), 21);
                    Part("pauldronR", Art.Circle, Palette.Iron, new Vector2(0f, -0.55f), new Vector2(0.55f, 0.55f), 21);
                    Part("head", Art.Circle, Palette.OrcSkin, new Vector2(0.12f, 0f), new Vector2(0.6f, 0.6f), 22);
                    Part("horn", Art.Triangle, Palette.Hex("#E8D9B0"), new Vector2(-0.35f, 0.5f), new Vector2(0.3f, 0.55f), 23, 30f);
                    Part("banner", Art.Square, Palette.Bad, new Vector2(-0.55f, -0.2f), new Vector2(0.12f, 0.9f), 19);
                    break;
                case EnemyType.Troll:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.7f, 1.7f), 20);
                    Part("belly", Art.Circle, Palette.Hex("#7A8A62"), new Vector2(0.15f, 0f), new Vector2(1.0f, 1.1f), 21);
                    Part("head", Art.Circle, Palette.Hex("#4A5A3C"), new Vector2(0.55f, 0f), new Vector2(0.55f, 0.55f), 22);
                    Part("tuskL", Art.Triangle, Palette.Hex("#F0E6D0"), new Vector2(0.78f, 0.12f), new Vector2(0.1f, 0.2f), 23, -90f);
                    Part("tuskR", Art.Triangle, Palette.Hex("#F0E6D0"), new Vector2(0.78f, -0.12f), new Vector2(0.1f, 0.2f), 23, -90f);
                    Part("club", Art.Square, Palette.Hex("#5A3A22"), new Vector2(0.2f, -0.85f), new Vector2(1.0f, 0.22f), 23);
                    break;
                case EnemyType.Gorruk:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.6f, 1.6f), 20);
                    Part("pauldronL", Art.Circle, Palette.Iron, new Vector2(0f, 0.62f), new Vector2(0.62f, 0.62f), 21);
                    Part("pauldronR", Art.Circle, Palette.Iron, new Vector2(0f, -0.62f), new Vector2(0.62f, 0.62f), 21);
                    Part("head", Art.Circle, Palette.OrcSkin, new Vector2(0.15f, 0f), new Vector2(0.7f, 0.7f), 22);
                    Part("helm", Art.Circle, Palette.Hex("#4A4542"), new Vector2(0.05f, 0f), new Vector2(0.55f, 0.6f), 23);
                    Part("hornL", Art.Triangle, Palette.Hex("#E8D9B0"), new Vector2(0.05f, 0.42f), new Vector2(0.22f, 0.4f), 24, 20f);
                    Part("hornR", Art.Triangle, Palette.Hex("#E8D9B0"), new Vector2(0.05f, -0.42f), new Vector2(0.22f, 0.4f), 24, 160f);
                    Part("axeHandle", Art.Square, Palette.Hex("#5A3A22"), new Vector2(0.55f, -0.75f), new Vector2(0.9f, 0.1f), 24);
                    Part("axeBlade", Art.Circle, Palette.Hex("#C8C2BA"), new Vector2(0.95f, -0.75f), new Vector2(0.45f, 0.6f), 25);
                    break;
                case EnemyType.Skeleton:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("ribs1", Art.Square, Palette.Hex("#6A6258"), new Vector2(-0.06f, 0f), new Vector2(0.05f, 0.38f), 21);
                    Part("ribs2", Art.Square, Palette.Hex("#6A6258"), new Vector2(0.06f, 0f), new Vector2(0.05f, 0.34f), 21);
                    Part("skull", Art.Circle, Palette.Hex("#F0EADC"), new Vector2(0.14f, 0f), new Vector2(0.3f, 0.3f), 22);
                    Part("eyes", Art.Square, Palette.Hex("#2A2622"), new Vector2(0.2f, 0f), new Vector2(0.06f, 0.16f), 23);
                    Part("blade", Art.Square, Palette.Hex("#9A948C"), new Vector2(0.2f, -0.28f), new Vector2(0.4f, 0.06f), 22);
                    break;
                case EnemyType.Ghoul:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2.2f, r * 1.8f), 20);
                    Part("hump", Art.Circle, Palette.Hex("#5E7052"), new Vector2(-0.12f, 0f), new Vector2(0.4f, 0.44f), 21);
                    Part("head", Art.Circle, Palette.Hex("#4E5E44"), new Vector2(0.24f, 0f), new Vector2(0.28f, 0.28f), 22);
                    Part("clawL", Art.Triangle, Palette.Hex("#E0D8C0"), new Vector2(0.3f, 0.24f), new Vector2(0.12f, 0.2f), 22, -90f);
                    Part("clawR", Art.Triangle, Palette.Hex("#E0D8C0"), new Vector2(0.3f, -0.24f), new Vector2(0.12f, 0.2f), 22, -90f);
                    break;
                case EnemyType.Wraith:
                    Part("trail", Art.Triangle, Palette.Hex("#6A8496"), new Vector2(-0.38f, 0f), new Vector2(0.4f, 0.55f), 19, 90f);
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("hood", Art.Circle, Palette.Hex("#22303A"), new Vector2(0.04f, 0f), new Vector2(0.42f, 0.42f), 21);
                    Part("eyes", Art.Square, Palette.Hex("#BFF6FF"), new Vector2(0.14f, 0f), new Vector2(0.06f, 0.2f), 22);
                    break;
                case EnemyType.Banshee:
                    Part("hair", Art.Circle, Palette.Hex("#5E6E86"), new Vector2(-0.12f, 0f), new Vector2(0.86f, 0.86f), 19);
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("face", Art.Circle, Palette.Hex("#EEF4FA"), new Vector2(0.12f, 0f), new Vector2(0.34f, 0.34f), 21);
                    Part("mouth", Art.Circle, Palette.Hex("#1E2630"), new Vector2(0.22f, 0f), new Vector2(0.1f, 0.14f), 22);
                    break;
                case EnemyType.Necromancer:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("hood", Art.Circle, Palette.Hex("#241C30"), new Vector2(0.05f, 0f), new Vector2(0.44f, 0.44f), 21);
                    Part("face", Art.Circle, Palette.Hex("#C8C0A8"), new Vector2(0.15f, 0f), new Vector2(0.18f, 0.18f), 22);
                    Part("staff", Art.Square, Palette.Hex("#5A4632"), new Vector2(0.15f, -0.34f), new Vector2(0.7f, 0.07f), 22);
                    Part("orb", Art.Circle, Palette.Hex("#8CFF7A"), new Vector2(0.5f, -0.34f), new Vector2(0.16f, 0.16f), 23);
                    break;
                case EnemyType.BoneGolem:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("plateL", Art.Square, Palette.Hex("#A8A08E"), new Vector2(0f, 0.34f), new Vector2(0.6f, 0.2f), 21, 10f);
                    Part("plateR", Art.Square, Palette.Hex("#A8A08E"), new Vector2(0f, -0.34f), new Vector2(0.6f, 0.2f), 21, -10f);
                    Part("skull", Art.Circle, Palette.Hex("#EAE2D0"), new Vector2(0.2f, 0f), new Vector2(0.44f, 0.44f), 22);
                    Part("eyes", Art.Square, Palette.Hex("#7AFF8A"), new Vector2(0.3f, 0f), new Vector2(0.07f, 0.24f), 23);
                    break;
                case EnemyType.DeathKnight:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.4f, 1.4f), 20);
                    Part("cape", Art.Circle, Palette.Hex("#4A1A22"), new Vector2(-0.3f, 0f), new Vector2(0.9f, 1.1f), 19);
                    Part("pauldronL", Art.Circle, Palette.Hex("#4A5260"), new Vector2(0f, 0.52f), new Vector2(0.5f, 0.5f), 21);
                    Part("pauldronR", Art.Circle, Palette.Hex("#4A5260"), new Vector2(0f, -0.52f), new Vector2(0.5f, 0.5f), 21);
                    Part("helm", Art.Circle, Palette.Hex("#1E2228"), new Vector2(0.12f, 0f), new Vector2(0.58f, 0.58f), 22);
                    Part("eyes", Art.Square, Palette.Hex("#FF4A3A"), new Vector2(0.3f, 0f), new Vector2(0.08f, 0.28f), 23);
                    Part("lance", Art.Square, Palette.Hex("#9AA2AE"), new Vector2(0.7f, -0.45f), new Vector2(1.6f, 0.1f), 23);
                    break;
                case EnemyType.Abomination:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.8f, 1.8f), 20);
                    Part("patch1", Art.Circle, Palette.Hex("#8A9A5E"), new Vector2(-0.3f, 0.35f), new Vector2(0.6f, 0.5f), 21);
                    Part("patch2", Art.Circle, Palette.Hex("#56663A"), new Vector2(0.15f, -0.4f), new Vector2(0.7f, 0.5f), 21);
                    Part("stitch", Art.Square, Palette.Hex("#2A2A1E"), new Vector2(0f, 0f), new Vector2(1.2f, 0.06f), 22, 30f);
                    Part("head", Art.Circle, Palette.Hex("#7A8A52"), new Vector2(0.6f, 0f), new Vector2(0.5f, 0.5f), 22);
                    Part("hook", Art.Triangle, Palette.Hex("#9A948C"), new Vector2(0.4f, -0.75f), new Vector2(0.3f, 0.4f), 23, -90f);
                    break;
                case EnemyType.Vesperine:
                    Part("aura", Art.Glow, new Color(0.6f, 0.4f, 1f, 0.5f), Vector2.zero, new Vector2(2.6f, 2.6f), 18);
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(1.5f, 1.5f), 20);
                    Part("robeTrim", Art.Ring, Palette.Hex("#B48CD9"), Vector2.zero, new Vector2(1.52f, 1.52f), 21);
                    Part("face", Art.Circle, Palette.Hex("#D8E0F0"), new Vector2(0.15f, 0f), new Vector2(0.5f, 0.5f), 22);
                    Part("crownL", Art.Triangle, Palette.Gold, new Vector2(-0.05f, 0.22f), new Vector2(0.18f, 0.28f), 23, 90f);
                    Part("crownM", Art.Triangle, Palette.Gold, new Vector2(-0.08f, 0f), new Vector2(0.2f, 0.34f), 23, 90f);
                    Part("crownR", Art.Triangle, Palette.Gold, new Vector2(-0.05f, -0.22f), new Vector2(0.18f, 0.28f), 23, 90f);
                    Part("staff", Art.Square, Palette.Hex("#3A2E4A"), new Vector2(0.3f, -0.6f), new Vector2(1.1f, 0.09f), 23);
                    Part("orb", Art.Glow, new Color(0.7f, 1f, 0.6f, 1f), new Vector2(0.85f, -0.6f), new Vector2(0.45f, 0.45f), 24);
                    break;
                default:
                    bodyRenderer = Part("body", Art.Circle, Def.Body, Vector2.zero, new Vector2(r * 2f, r * 2f), 20);
                    Part("head", Art.Circle, Palette.OrcSkin, new Vector2(0.05f, 0f), new Vector2(0.34f, 0.34f), 21);
                    Part("blade", Art.Square, Palette.Hex("#B8B2AA"), new Vector2(0.22f, -0.3f), new Vector2(0.45f, 0.08f), 22);
                    break;
            }
            bodyColor = bodyRenderer.color;

            stunMark = Art.Make("stun", transform, Art.Ring, Palette.Gold, new Vector2(0f, r + 0.15f), new Vector2(0.45f, 0.2f), 52);
            stunMark.enabled = false;
            staggerMark = Art.Make("stagger", transform, Art.Ring, Palette.Interrupt, Vector2.zero, Vector2.one * r * 2.8f, 19);
            staggerMark.enabled = false;
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
            UpdateVisibility();

            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                return;
            }
            if (Def.SummonInterval > 0f)
            {
                summonTimer -= dt;
                if (summonTimer <= 0f)
                {
                    summonTimer = Def.SummonInterval;
                    var lmS = LevelManager.I;
                    if (lmS != null)
                    {
                        lmS.SpawnAt(Def.SummonType, Distance - Def.Radius - 0.5f, SpawnWave, true);
                        Fx.Burst(Position, new Color(0.55f, 0.75f, 0.35f, 0.8f), 0.6f, 1.8f, 0.4f);
                    }
                }
            }
            if (!CanMove) return;

            if (BlockedBy != null && IgnoresBlock)
            {
                var h = BlockedBy;
                h.Release(this);
                h.TakeDamage(60f);
                if (LevelManager.I != null) LevelManager.I.AddPopup(h.Position + Vector2.up, "Trampled!", Palette.Bad);
            }
            if (BlockedBy != null)
            {
                if (BlockedBy.Dead) BlockedBy = null;
                else
                {
                    BlockedBy.TakeDamage(Def.HeroDps * (1f + EnrageFrac) * dt);
                    Face(BlockedBy.Position - Position);
                    return;
                }
            }

            Move(dt);
        }

        protected void Move(float dt)
        {
            float burst = Def.Bursty ? (Mathf.Repeat(Time.time * 0.5f + bob, 1f) < 0.35f ? 2.5f : 0.75f) : 1f;
            float speed = Def.Speed * slowMult * SpeedBuff * (1f + 1.5f * EnrageFrac) * burst * SpeedMultiplier;
            Distance += speed * dt;
            transform.position = PositionAt(Distance);
            Face(path.DirectionAt(Distance));
            if (body != null) body.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin((Time.time + bob) * speed * 6f));
            if (Distance >= path.Length) ReachGate();
        }

        void UpdateVisibility()
        {
            if (!Def.Invisible && !forceHidden)
            {
                if (IsHidden)
                {
                    IsHidden = false;
                    SetPartAlpha(1f);
                }
                return;
            }
            bool hidden = revealTimer <= 0f && BlockedBy == null;
            if (hidden)
            {
                var lm = LevelManager.I;
                if (lm != null && lm.HeroUnit != null && !lm.HeroUnit.Dead && (lm.HeroUnit.Position - Position).sqrMagnitude <= lm.HeroUnit.RevealRadius * lm.HeroUnit.RevealRadius)
                    hidden = false;
            }
            IsHidden = hidden;
            SetPartAlpha(hidden ? 0.28f + 0.1f * Mathf.Sin(Time.time * 9f + bob) : 1f);
        }

        void SetPartAlpha(float a)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == bodyRenderer) continue;
                Color c = partColors[i];
                c.a = partColors[i].a * a;
                parts[i].color = c;
            }
            Color bc = bodyRenderer.color;
            bc.a = a;
            bodyRenderer.color = bc;
        }

        void TickEffects(float dt)
        {
            if (slowTimer > 0f) { slowTimer -= dt; if (slowTimer <= 0f) slowMult = 1f; }
            if (markTimer > 0f) markTimer -= dt;
            if (dispelTimer > 0f) dispelTimer -= dt;
            if (staggerTimer > 0f) staggerTimer -= dt;
            if (revealTimer > 0f) revealTimer -= dt;
            sinceHit += dt;

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

            // Troll regeneration
            if (Def.Regen > 0f && sinceHit > 2f && Hp < MaxHp)
            {
                Hp = Mathf.Min(MaxHp, Hp + Def.Regen * GameSession.EnemyHealthMult * dt);
                bar.Set(Hp / MaxHp);
                regenFx -= dt;
                if (regenFx <= 0f)
                {
                    regenFx = 0.2f;
                    Fx.Burst(Position + Random.insideUnitCircle * Def.Radius, new Color(0.5f, 1f, 0.4f, 0.8f), 0.3f, 0.05f, 0.6f, Art.Circle, 46, new Vector3(0f, 1f, 0f));
                }
            }

            if (flashTimer > 0f) flashTimer -= dt;
            Color c = bodyColor;
            if (slowTimer > 0f) c = Color.Lerp(c, Palette.Frost, 0.5f);
            if (markTimer > 0f) c = Color.Lerp(c, Palette.Gold, 0.25f);
            if (Def.Berserk) c = Color.Lerp(c, new Color(1f, 0.15f, 0.05f), EnrageFrac * 0.6f);
            if (flashTimer > 0f) c = Color.Lerp(c, Color.white, 0.7f);
            c.a = bodyRenderer.color.a;
            bodyRenderer.color = c;

            stunMark.enabled = stunTimer > 0f;
            if (stunMark.enabled) stunMark.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 360f);
            staggerMark.enabled = staggerTimer > 0f;
            if (staggerMark.enabled) staggerMark.color = new Color(Palette.Interrupt.r, Palette.Interrupt.g, Palette.Interrupt.b, 0.4f + 0.4f * Mathf.Sin(Time.time * 12f));

            float rage = Mathf.Max(SpeedBuff > 1.01f ? 0.55f : 0f, EnrageFrac * 0.8f);
            frenzy.enabled = rage > 0.05f && !IsHidden;
            if (frenzy.enabled)
            {
                frenzy.color = new Color(1f, 0.25f, 0.1f, rage);
                frenzy.transform.localScale = Vector3.one * Def.Radius * (3.2f + 0.3f * Mathf.Sin(Time.time * 14f));
            }
            if (aura != null)
            {
                aura.enabled = AuraActive;
                aura.transform.localScale = Vector3.one * Balance.DrummerAuraRadius * 2f * (1f + 0.03f * Mathf.Sin(Time.time * 8f));
            }
        }

        protected void Face(Vector2 dir)
        {
            if (body == null || dir.sqrMagnitude < 0.0001f) return;
            if (usesArt) { UpdateFacing(dir); return; }   // art stays upright; swap/flip the sprite instead
            body.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>Art stays upright: swap to the north/east sprite if we have one, flip for west.</summary>
        void UpdateFacing(Vector2 dir)
        {
            if (!usesArt || dir.sqrMagnitude < 0.00001f) return;

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                bodyRenderer.sprite = artE != null ? artE : artS;   // side view, or south art if no east art yet
                bodyRenderer.flipX = dir.x < 0f;                    // west = mirrored east
            }
            else
            {
                bodyRenderer.sprite = (dir.y > 0f && artN != null) ? artN : artS;
                bodyRenderer.flipX = false;
            }
        }

        public virtual void TakeDamage(float amount, DamageKind kind, Tower source = null, bool byHero = false)
        {
            if (!Alive || amount <= 0f) return;
            if (source != null) { LastTowerHit = source; LastHitByHero = false; }
            if (byHero) LastHitByHero = true;
            if (kind == DamageKind.Physical) amount *= 1f - Def.Armor;
            if (markTimer > 0f) amount *= 1.15f;
            if (staggerTimer > 0f) amount *= Balance.StaggerDamageMult;
            if (Def.Ethereal && IsHidden && kind != DamageKind.Magic) amount *= 0.5f;
            Hp -= amount;
            sinceHit = 0f;
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

        public void Reveal(float duration) { if (Alive) revealTimer = Mathf.Max(revealTimer, duration); }

        /// <summary>Stun. If the enemy is winding up an ability, the stun INTERRUPTS it.</summary>
        public void Stun(float duration)
        {
            if (!Alive) return;
            if (WindingUp) Interrupt();
            if (IsBoss) duration *= 0.5f;
            stunTimer = Mathf.Max(stunTimer, duration);
        }

        void Interrupt()
        {
            OnInterrupted();
            staggerTimer = Balance.StaggerTime;
            Fx.Ring(Position, Palette.Interrupt, 0.4f, 2.4f, 0.45f);
            Fx.Sparks(Position, Palette.Interrupt, 14, 4f, 0.5f);
            AudioManager.Play(Sfx.Interrupt);
            var lm = LevelManager.I;
            if (lm != null) lm.OnInterrupt(this);
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

        public void Heal(float amount)
        {
            if (!Alive) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            bar.Set(Hp / MaxHp);
        }

        protected virtual void Die()
        {
            if (removed) return;
            removed = true;
            Hp = 0f;
            if (BlockedBy != null) BlockedBy.Release(this);
            Corpse.Spawn(Position, bodyColor, Def.Radius);
            Fx.Burst(Position, new Color(0.9f, 0.4f, 0.2f, 0.9f), Def.Radius * 1.5f, Def.Radius * 4f, 0.35f);
            Fx.Sparks(Position, new Color(1f, 0.75f, 0.35f, 1f), IsBoss ? 24 : 6, IsBoss ? 6f : 3f, 0.45f);
            AudioManager.Play(Def.Undead ? Sfx.Bones : Sfx.Die, IsBoss ? 1f : 0.45f);
            var lm = LevelManager.I;
            if (lm != null && Def.SplitCount > 0)
            {
                // Bone Golem / Abomination break apart into smaller undead.
                for (int i = 0; i < Def.SplitCount; i++)
                    lm.SpawnAt(Def.SplitType, Distance - 0.35f * i, SpawnWave, true);
                Fx.Ring(Position, new Color(0.85f, 0.85f, 0.7f, 0.8f), 0.4f, 2f, 0.4f);
            }
            if (lm != null) lm.EnemyKilled(this);
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

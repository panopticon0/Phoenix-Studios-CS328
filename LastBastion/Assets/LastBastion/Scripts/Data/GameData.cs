using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    public enum EnemyType
    {
        // Orc Warhost (Level 1)
        Grunt, WolfRider, WarDrummer, Ironhide, GoblinSneak, Berserker, Brakka, Troll, Gorruk,
        // Undead Legion (Level 2)
        Skeleton, Ghoul, Wraith, Banshee, Necromancer, BoneGolem, DeathKnight, Abomination, Vesperine
    }
    public enum LevelTheme { Ashen, Marsh }
    public enum TowerType { Archer, Catapult, Mage }
    public enum DamageKind { Physical, Magic, True }
    public enum TargetPriority { First, Last, Strong, Close }

    /// <summary>Game-wide tuning numbers. Change these to rebalance the beta.</summary>
    public static class Balance
    {
        public const int StartGold = 300;
        public const float FirstWaveDelay = 30f;   // seconds before wave 1 starts on its own
        public const float WaveBreak = 12f;        // countdown between waves
        public const int EarlyBonusPerSecond = 3;  // gold per second skipped when calling a wave early
        public const float SellRefund = 0.7f;
        public const float DrummerAuraRadius = 3.5f;
        public const float DrummerTowerSlow = 0.7f; // towers attack at 70% speed (30% slower)
        public const float WarCrySpeed = 1.5f;      // Gorruk's War Cry: +50% speed

        // Free tower placement
        public const float TowerFootprint = 0.68f;  // tower radius; towers must be 2x this apart
        public const float MinRoadDistance = 1.5f;  // keeps towers off the road (road half-width 0.85)
        public const float MaxRoadDistance = 4.5f;  // towers must be built "along the path"

        // New combat mechanic: interrupts
        public const float StaggerTime = 3f;          // after an interrupt the enemy is Staggered...
        public const float StaggerDamageMult = 1.25f; // ...and takes +25% damage

        // Invisible enemies
        public const float HeroRevealRadius = 2.4f;   // Sir Aldric spots Goblin Sneaks this close

        // Hazards
        public const float AshStormRangeMult = 0.6f;  // towers in the ash cloud lose 40% range
        public const float RockslideRecharge = 40f;
        public const float RockslideDamage = 140f;

        // Power-up: Dragonfire
        public const int DragonfireMaxCharges = 3;
        public const int DragonfireStartCharges = 1;
        public const float DragonfireRadius = 2.6f;
        public const float DragonfireDamage = 180f;
        public const float DragonfireBurnDps = 15f;
        public const float DragonfireBurnTime = 4f;
        public const float DragonfireRevealTime = 6f;

        public static int WaveBounty(int waveNumber) => 10 + 4 * waveNumber;

        /// <summary>Non-boss enemies get tougher every wave (wave 30 = 2.3x health).</summary>
        public static float WaveHealthScale(int waveNumber) => 1f + 0.045f * Mathf.Max(0, waveNumber - 1);
    }

    public class EnemyDef
    {
        public string Name, ShortName, Intro, Mechanic;
        public float MaxHp, Speed, Armor, HeroDps, Radius;
        public int Gold, LifeCost;
        public Color Body;
        public bool IsBoss, Invisible, Berserk;
        public float Regen;        // hp/s after 2s without taking damage
        public float ShardChance;  // chance to drop an Ember Shard (Dragonfire charge)

        // Undead mechanics
        public bool Undead;
        public bool Bursty;        // Ghoul: lurches forward in bursts of speed
        public bool Ethereal;      // Wraith: non-magic damage halved while unseen
        public EnemyType SplitType;   // Bone Golem / Abomination: breaks apart on death...
        public int SplitCount;        // ...into this many of SplitType
        public EnemyType SummonType;  // Abomination: sheds a minion every SummonInterval seconds
        public float SummonInterval;
    }

    public static class EnemyData
    {
        static readonly EnemyDef grunt = new EnemyDef
        {
            Name = "Orc Grunt", ShortName = "Grunts", Mechanic = "Basic infantry",
            Intro = "Basic infantry in large numbers.",
            MaxHp = 60f, Speed = 1.6f, Armor = 0f, HeroDps = 8f, Radius = 0.32f, Gold = 5, LifeCost = 1,
            Body = Palette.OrcArmor, ShardChance = 0.02f
        };

        static readonly EnemyDef wolf = new EnemyDef
        {
            Name = "Wolf Rider", ShortName = "Wolf Riders", Mechanic = "Twice as fast",
            Intro = "Wolf Riders move twice as fast as Grunts. Block them with Sir Aldric, or slow them with Tar Pots and Frost Bolts.",
            MaxHp = 45f, Speed = 3.2f, Armor = 0f, HeroDps = 10f, Radius = 0.38f, Gold = 7, LifeCost = 1,
            Body = Palette.Hex("#7A746C"), ShardChance = 0.06f
        };

        static readonly EnemyDef drummer = new EnemyDef
        {
            Name = "War Drummer", ShortName = "War Drummers", Mechanic = "Slows nearby towers 30%",
            Intro = "War Drummer! Its drums slow every tower within range by 30%. Kill it first, or hit it with a Mage Tower's Dispel to silence it.",
            MaxHp = 130f, Speed = 1.3f, Armor = 0.1f, HeroDps = 6f, Radius = 0.42f, Gold = 14, LifeCost = 2,
            Body = Palette.Hex("#8A3A20"), ShardChance = 0.25f
        };

        static readonly EnemyDef ironhide = new EnemyDef
        {
            Name = "Ironhide", ShortName = "Ironhides", Mechanic = "Heavy armor (-50% physical)",
            Intro = "Ironhides wear heavy plate: arrows, stones and swords do half damage. Mage Towers and Ballista Bolts cut straight through.",
            MaxHp = 150f, Speed = 1.15f, Armor = 0.5f, HeroDps = 10f, Radius = 0.42f, Gold = 11, LifeCost = 2,
            Body = Palette.Hex("#5C5650"), ShardChance = 0.12f
        };

        static readonly EnemyDef goblin = new EnemyDef
        {
            Name = "Goblin Sneak", ShortName = "Goblin Sneaks", Mechanic = "Invisible",
            Intro = "Goblin Sneaks are invisible! Towers need Eagle Eye (Archer), True Sight (Mage) or Spotter Crow (Catapult) to target them. Sir Aldric, splash damage and Dragonfire still hit them.",
            MaxHp = 42f, Speed = 2.3f, Armor = 0f, HeroDps = 7f, Radius = 0.28f, Gold = 9, LifeCost = 1,
            Body = Palette.Hex("#5E7A2E"), Invisible = true, ShardChance = 0.12f
        };

        static readonly EnemyDef berserker = new EnemyDef
        {
            Name = "Berserker", ShortName = "Berserkers", Mechanic = "Faster and deadlier as it is hurt",
            Intro = "Berserkers ENRAGE as they take damage: up to 2.5x speed when nearly dead. Finish them fast with burst damage or a stun.",
            MaxHp = 170f, Speed = 1.35f, Armor = 0.1f, HeroDps = 14f, Radius = 0.44f, Gold = 15, LifeCost = 2,
            Body = Palette.Hex("#B8361E"), Berserk = true, ShardChance = 0.2f
        };

        static readonly EnemyDef brakka = new EnemyDef
        {
            Name = "Warchief Brakka", ShortName = "Brakka", Mechanic = "Rally Horn heals nearby orcs (interruptible)",
            Intro = "Warchief Brakka! He winds up a Rally Horn that heals every nearby orc. STUN him during the wind-up (Shield Slam!) to INTERRUPT it.",
            MaxHp = 1100f, Speed = 1.0f, Armor = 0.35f, HeroDps = 22f, Radius = 0.7f, Gold = 80, LifeCost = 6,
            Body = Palette.Hex("#6E3A22"), IsBoss = true, ShardChance = 1f
        };

        static readonly EnemyDef troll = new EnemyDef
        {
            Name = "Troll Juggernaut", ShortName = "Troll", Mechanic = "Regenerates when not hit for 2s",
            Intro = "Troll Juggernaut! It regenerates quickly whenever it goes 2 seconds without taking damage. Keep the pressure on.",
            MaxHp = 3000f, Speed = 0.75f, Armor = 0.2f, HeroDps = 30f, Radius = 0.85f, Gold = 120, LifeCost = 8,
            Body = Palette.Hex("#5A6E4A"), IsBoss = true, Regen = 45f, ShardChance = 1f
        };

        static readonly EnemyDef gorruk = new EnemyDef
        {
            Name = "Gorruk Skullsplitter", ShortName = "Gorruk", Mechanic = "Axe Throw and War Cry (both interruptible)",
            Intro = "Gorruk Skullsplitter, Orc Warlord! He hurls his axe to knock a tower out for 5 seconds, and at half health he winds up a War Cry that drives every orc 50% faster. Stun him during either wind-up to interrupt it.",
            MaxHp = 4200f, Speed = 0.9f, Armor = 0.3f, HeroDps = 35f, Radius = 0.8f, Gold = 200, LifeCost = 10,
            Body = Palette.Hex("#7A2E1A"), IsBoss = true, ShardChance = 1f
        };

        // ------------------------------------------------------------ Undead Legion (Level 2)

        static readonly EnemyDef skeleton = new EnemyDef
        {
            Name = "Skeleton", ShortName = "Skeletons", Mechanic = "Weak but relentless",
            Intro = "Skeletons: weak, but they never stop coming.",
            MaxHp = 50f, Speed = 1.7f, Armor = 0f, HeroDps = 7f, Radius = 0.3f, Gold = 4, LifeCost = 1,
            Body = Palette.Hex("#D8D0BE"), Undead = true, ShardChance = 0.02f
        };

        static readonly EnemyDef ghoul = new EnemyDef
        {
            Name = "Ghoul", ShortName = "Ghouls", Mechanic = "Lurches in bursts of speed",
            Intro = "Ghouls shamble, then LURCH forward at 2.5x speed every couple of seconds. Slows and Sir Aldric's block take the bite out of their bursts.",
            MaxHp = 70f, Speed = 1.4f, Armor = 0f, HeroDps = 12f, Radius = 0.34f, Gold = 6, LifeCost = 1,
            Body = Palette.Hex("#7A8F6A"), Undead = true, Bursty = true, ShardChance = 0.06f
        };

        static readonly EnemyDef wraith = new EnemyDef
        {
            Name = "Wraith", ShortName = "Wraiths", Mechanic = "Invisible; halves non-magic damage while unseen",
            Intro = "Wraiths are invisible AND ethereal: while unseen, arrows and stones do half damage. Give towers sight, and bring Mage Towers. Dragonfire and the marsh gas reveal them.",
            MaxHp = 64f, Speed = 1.9f, Armor = 0f, HeroDps = 9f, Radius = 0.34f, Gold = 10, LifeCost = 1,
            Body = Palette.Hex("#9FB8C8"), Undead = true, Invisible = true, Ethereal = true, ShardChance = 0.12f
        };

        static readonly EnemyDef banshee = new EnemyDef
        {
            Name = "Banshee", ShortName = "Banshees", Mechanic = "Wail freezes a tower for 3s (interruptible)",
            Intro = "Banshee! She winds up a wail that FREEZES the nearest tower for 3 seconds. Stun her during the wind-up to interrupt it.",
            MaxHp = 120f, Speed = 1.25f, Armor = 0f, HeroDps = 6f, Radius = 0.38f, Gold = 14, LifeCost = 2,
            Body = Palette.Hex("#C8D8E8"), Undead = true, ShardChance = 0.25f
        };

        static readonly EnemyDef necromancer = new EnemyDef
        {
            Name = "Necromancer", ShortName = "Necromancers", Mechanic = "Raises Skeletons (interruptible)",
            Intro = "Necromancers stop to RAISE two Skeletons from the mud every few seconds. Kill them first, or stun the ritual to interrupt it.",
            MaxHp = 140f, Speed = 1.1f, Armor = 0.1f, HeroDps = 6f, Radius = 0.4f, Gold = 16, LifeCost = 2,
            Body = Palette.Hex("#3A2E4A"), Undead = true, ShardChance = 0.25f
        };

        static readonly EnemyDef boneGolem = new EnemyDef
        {
            Name = "Bone Golem", ShortName = "Bone Golems", Mechanic = "Armored; shatters into 4 Skeletons",
            Intro = "Bone Golems are armored, and when one falls it SHATTERS into four Skeletons. Splash damage cleans up the pieces.",
            MaxHp = 320f, Speed = 0.95f, Armor = 0.3f, HeroDps = 14f, Radius = 0.55f, Gold = 20, LifeCost = 3,
            Body = Palette.Hex("#CFC6B2"), Undead = true, SplitType = EnemyType.Skeleton, SplitCount = 4, ShardChance = 0.3f
        };

        static readonly EnemyDef deathKnight = new EnemyDef
        {
            Name = "Death Knight Varn", ShortName = "Varn", Mechanic = "Death Charge (interruptible)",
            Intro = "Death Knight Varn! He lowers his lance and winds up a DEATH CHARGE: a burst of speed that tramples Sir Aldric. Stun him during the wind-up to interrupt it.",
            MaxHp = 1300f, Speed = 0.95f, Armor = 0.45f, HeroDps = 26f, Radius = 0.7f, Gold = 90, LifeCost = 6,
            Body = Palette.Hex("#2E3440"), Undead = true, IsBoss = true, ShardChance = 1f
        };

        static readonly EnemyDef abomination = new EnemyDef
        {
            Name = "Plague Abomination", ShortName = "Abomination", Mechanic = "Sheds Ghouls; bursts into 5 on death",
            Intro = "Plague Abomination! It sheds a Ghoul every few seconds, and when it dies it bursts into five more. Have splash ready.",
            MaxHp = 3400f, Speed = 0.7f, Armor = 0.15f, HeroDps = 28f, Radius = 0.9f, Gold = 130, LifeCost = 8,
            Body = Palette.Hex("#6E7F4A"), Undead = true, IsBoss = true, ShardChance = 1f,
            SplitType = EnemyType.Ghoul, SplitCount = 5, SummonType = EnemyType.Ghoul, SummonInterval = 6f
        };

        static readonly EnemyDef vesperine = new EnemyDef
        {
            Name = "Vesperine, the Lich Queen", ShortName = "Vesperine", Mechanic = "Fades from sight (interruptible); Bone Wall",
            Intro = "Vesperine, the Lich Queen! She winds up a FADE that makes her invisible (stun her to interrupt it, or bring sight). Each time she loses a quarter of her health she raises a Bone Wall of Skeletons.",
            MaxHp = 4600f, Speed = 0.85f, Armor = 0.25f, HeroDps = 30f, Radius = 0.75f, Gold = 220, LifeCost = 10,
            Body = Palette.Hex("#4A3A6E"), Undead = true, IsBoss = true, ShardChance = 1f
        };

        public static EnemyDef Get(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Skeleton: return skeleton;
                case EnemyType.Ghoul: return ghoul;
                case EnemyType.Wraith: return wraith;
                case EnemyType.Banshee: return banshee;
                case EnemyType.Necromancer: return necromancer;
                case EnemyType.BoneGolem: return boneGolem;
                case EnemyType.DeathKnight: return deathKnight;
                case EnemyType.Abomination: return abomination;
                case EnemyType.Vesperine: return vesperine;
                case EnemyType.WolfRider: return wolf;
                case EnemyType.WarDrummer: return drummer;
                case EnemyType.Ironhide: return ironhide;
                case EnemyType.GoblinSneak: return goblin;
                case EnemyType.Berserker: return berserker;
                case EnemyType.Brakka: return brakka;
                case EnemyType.Troll: return troll;
                case EnemyType.Gorruk: return gorruk;
                default: return grunt;
            }
        }
    }

    public class UpgradeDef
    {
        public string Name, Desc;
        public UpgradeDef(string name, string desc) { Name = name; Desc = desc; }
    }

    public class TowerDef
    {
        public TowerType Type;
        public string Name, Role, Blurb;
        public int Cost;
        public float Damage, Interval, Range, Splash;
        public DamageKind Kind;
        public Color Roof;
        /// <summary>[branch, tier-1]: branch 0 Power, 1 Speed, 2 Utility.</summary>
        public UpgradeDef[,] Upgrades;
    }

    public static class TowerData
    {
        public static readonly string[] BranchNames = { "Power", "Speed", "Utility" };
        public static readonly TowerType[] BuildOrder = { TowerType.Archer, TowerType.Catapult, TowerType.Mage };

        static readonly TowerDef archer = new TowerDef
        {
            Type = TowerType.Archer, Name = "Archer Tower", Role = "Fast single-target",
            Blurb = "Cheap and quick. Weak against Ironhide armor until Ballista Bolts.",
            Cost = 100, Damage = 12f, Interval = 0.85f, Range = 4.5f, Splash = 0f, Kind = DamageKind.Physical,
            Roof = Palette.Hex("#4E7A3A"),
            Upgrades = new UpgradeDef[,]
            {
                { new UpgradeDef("Steel Tips", "+20% damage."),
                  new UpgradeDef("Piercing Arrows", "Arrows also hit a second enemy behind the target."),
                  new UpgradeDef("Ballista Bolts", "+30% damage and arrows ignore armor.") },
                { new UpgradeDef("Quick Draw", "+20% attack speed."),
                  new UpgradeDef("Twin Bows", "+40% attack speed."),
                  new UpgradeDef("Arrow Storm", "Every 8s, loose a volley of 6 arrows.") },
                { new UpgradeDef("Eagle Eye", "+20% range. Can target invisible Goblin Sneaks."),
                  new UpgradeDef("Hunter's Mark", "Hit enemies take +15% damage from everything for 4s."),
                  new UpgradeDef("Flame Arrows", "Arrows set enemies on fire (6 dmg/s for 3s).") }
            }
        };

        static readonly TowerDef catapult = new TowerDef
        {
            Type = TowerType.Catapult, Name = "Catapult", Role = "Heavy splash, slow",
            Blurb = "Lobbed stones crush groups, but can miss fast enemies. Splash hits invisible enemies too.",
            Cost = 175, Damage = 34f, Interval = 3.0f, Range = 5.5f, Splash = 1.3f, Kind = DamageKind.Physical,
            Roof = Palette.Hex("#8A5A32"),
            Upgrades = new UpgradeDef[,]
            {
                { new UpgradeDef("Heavy Stones", "+25% damage."),
                  new UpgradeDef("Boulders", "Splash radius +40%."),
                  new UpgradeDef("Earthshaker", "Impacts stun enemies for 1s (interrupts boss wind-ups).") },
                { new UpgradeDef("Counterweights", "+20% attack speed."),
                  new UpgradeDef("Twin Arms", "+40% attack speed."),
                  new UpgradeDef("Siege Engine", "Fires 3 stones per volley.") },
                { new UpgradeDef("Tar Pots", "Impacts slow enemies by 30% for 2s."),
                  new UpgradeDef("Firepots", "Impacts burn enemies (8 dmg/s for 3s)."),
                  new UpgradeDef("Spotter Crow", "+30% range. Can target invisible Goblin Sneaks.") }
            }
        };

        static readonly TowerDef mage = new TowerDef
        {
            Type = TowerType.Mage, Name = "Mage Tower", Role = "Magic ignores armor",
            Blurb = "Short range, but magic bolts ignore armor. The answer to Ironhides and bosses.",
            Cost = 150, Damage = 22f, Interval = 1.4f, Range = 3.6f, Splash = 0f, Kind = DamageKind.Magic,
            Roof = Palette.Hex("#6A4A99"),
            Upgrades = new UpgradeDef[,]
            {
                { new UpgradeDef("Focus Crystal", "+20% damage."),
                  new UpgradeDef("Arcane Chain", "Bolts chain to 2 more enemies for 60% damage."),
                  new UpgradeDef("Meteor", "Every 10s, call a meteor (150 magic damage, area).") },
                { new UpgradeDef("Swift Runes", "+20% attack speed."),
                  new UpgradeDef("Twin Orbs", "+40% attack speed."),
                  new UpgradeDef("Overcharge", "Every 12s, attack twice as fast for 4s.") },
                { new UpgradeDef("Frost Bolt", "Hits slow enemies by 25% for 2s."),
                  new UpgradeDef("True Sight", "+20% range. Can target invisible Goblin Sneaks."),
                  new UpgradeDef("Dispel", "Hits strip buffs (War Cry) and silence War Drummers for 3s.") }
            }
        };

        public static TowerDef Get(TowerType t)
        {
            switch (t)
            {
                case TowerType.Catapult: return catapult;
                case TowerType.Mage: return mage;
                default: return archer;
            }
        }

        public static int UpgradeCost(TowerType t, int tier)
        {
            int c = tier <= 1 ? 70 : tier == 2 ? 120 : 200;
            if (t == TowerType.Catapult) c = Mathf.RoundToInt(c * 1.2f / 5f) * 5;
            return c;
        }

        public static string PriorityName(TargetPriority p)
        {
            switch (p)
            {
                case TargetPriority.Last: return "Last";
                case TargetPriority.Strong: return "Strong";
                case TargetPriority.Close: return "Close";
                default: return "First";
            }
        }
    }

    /// <summary>A playable hero: stats, ability and three-branch level-up tree.</summary>
    public class HeroDef
    {
        public int Id;
        public string Name, ShortName, Title, Blurb, Role;
        public float BaseHp, Speed, Damage, AttackInterval, Range, EngageRadius, RevealRadius;
        public int MaxBlock;                 // 0 = ranged, doesn't block
        public string AbilityName, AbilityDesc;
        public float AbilityCooldown, AbilityRadius, AbilityDamage, AbilityStun;
        public Color Body, Cloak, Trim, Glow;
        public string[] BranchNames;
        public UpgradeDef[,] Tree;
        public bool Ranged => MaxBlock == 0;
    }

    /// <summary>Heroes and the shared XP curve. One hero point per level (levels 2-10 = 9 points).</summary>
    public static class HeroData
    {
        public const int AldricId = 0;
        public const int KaelaId = 1;
        public const int MaxLevel = 10;

        /// <summary>Total XP needed to reach level (index + 1).</summary>
        public static readonly int[] LevelXp = { 0, 40, 100, 180, 280, 400, 550, 720, 920, 1150 };

        public static readonly HeroDef Aldric = new HeroDef
        {
            Id = AldricId, Name = "Sir Aldric the Unbroken", ShortName = "Sir Aldric", Title = "Shield Knight",
            Role = "Melee blocker",
            Blurb = "A shield-bearing knight. Stands in the road, blocks up to 3 enemies and fights them in melee. Shield Slam stuns everything around him.",
            BaseHp = 420f, Speed = 3.8f, Damage = 20f, AttackInterval = 0.75f, Range = 0f, EngageRadius = 1.4f, RevealRadius = 2.4f, MaxBlock = 3,
            AbilityName = "Shield Slam", AbilityDesc = "Damages and stuns every enemy around him. Stuns INTERRUPT boss wind-ups.",
            AbilityCooldown = 14f, AbilityRadius = 2.6f, AbilityDamage = 45f, AbilityStun = 2f,
            Body = Palette.HeroBlue, Cloak = Palette.Hex("#B0301E"), Trim = Palette.Gold, Glow = Palette.HeroGlow,
            BranchNames = new[] { "Valor", "Bulwark", "Command" },
            Tree = new UpgradeDef[,]
            {
                { new UpgradeDef("Keen Edge", "+25% sword damage."),
                  new UpgradeDef("Cleave", "Each swing hits every enemy he is blocking."),
                  new UpgradeDef("Holy Blade", "+25% more damage, and strikes ignore armor.") },
                { new UpgradeDef("Thick Plate", "+30% max health."),
                  new UpgradeDef("Shield Wall", "Blocks 4 enemies at once and takes 25% less damage."),
                  new UpgradeDef("Last Stand", "Once per life, survives a killing blow with 30% health and 3s of invulnerability.") },
                { new UpgradeDef("Drill Master", "Shield Slam cooldown -25%."),
                  new UpgradeDef("Thunder Slam", "Shield Slam radius +40% and stuns for 3s."),
                  new UpgradeDef("Rally Cry", "Shield Slam also gives towers within 5 units +30% attack speed for 6s.") }
            }
        };

        public static readonly HeroDef Kaela = new HeroDef
        {
            Id = KaelaId, Name = "Kaela Swiftwind", ShortName = "Kaela", Title = "Ranger",
            Role = "Ranged sharpshooter",
            Blurb = "A ranger who shoots from a distance instead of blocking. Her keen eyes reveal invisible enemies in a wide area. Pinning Volley rains arrows that pin (stun) a whole group.",
            BaseHp = 260f, Speed = 4.4f, Damage = 16f, AttackInterval = 0.6f, Range = 4.6f, EngageRadius = 0f, RevealRadius = 4.4f, MaxBlock = 0,
            AbilityName = "Pinning Volley", AbilityDesc = "Rains arrows on the enemies she's aiming at: damage plus a pinning stun that INTERRUPTS boss wind-ups.",
            AbilityCooldown = 16f, AbilityRadius = 2.4f, AbilityDamage = 40f, AbilityStun = 1.5f,
            Body = Palette.Hex("#5E7A3A"), Cloak = Palette.Hex("#2E4A2A"), Trim = Palette.Hex("#C8A46A"), Glow = Palette.Hex("#C8F07A"),
            BranchNames = new[] { "Marksman", "Fleetfoot", "Huntress" },
            Tree = new UpgradeDef[,]
            {
                { new UpgradeDef("Broadheads", "+25% arrow damage."),
                  new UpgradeDef("Piercing Shot", "Arrows also hit a second enemy behind the target."),
                  new UpgradeDef("Elven Arrows", "+25% more damage, and arrows ignore armor.") },
                { new UpgradeDef("Quick Hands", "+25% attack speed."),
                  new UpgradeDef("Long Stride", "+20% range and 30% faster movement."),
                  new UpgradeDef("Twin Shot", "Looses two arrows per attack at different enemies.") },
                { new UpgradeDef("Trapper", "Pinning Volley cooldown -25%."),
                  new UpgradeDef("Barbed Volley", "Volley radius +40% and pins for 2.5s."),
                  new UpgradeDef("Hunter's Mark", "Volley marks enemies (+15% damage taken for 6s) and reveals them for 8s.") }
            }
        };

        public static readonly HeroDef[] All = { Aldric, Kaela };

        public static HeroDef Get(int id) => id == KaelaId ? Kaela : Aldric;
    }

    public class SpawnGroup
    {
        public EnemyType Type;
        public int Count;
        public float Interval, Delay;
        public SpawnGroup(EnemyType type, int count, float interval, float delay = 0f)
        {
            Type = type; Count = count; Interval = interval; Delay = delay;
        }
    }

    public class WaveDef
    {
        public SpawnGroup[] Groups;
        public WaveDef(params SpawnGroup[] groups) { Groups = groups; }

        public bool HasBoss
        {
            get
            {
                foreach (var g in Groups) if (EnemyData.Get(g.Type).IsBoss) return true;
                return false;
            }
        }

        /// <summary>"10 Grunts, 1 War Drummer" style summary for the wave preview.</summary>
        public string Summary()
        {
            var order = new List<EnemyType>();
            var counts = new Dictionary<EnemyType, int>();
            foreach (var g in Groups)
            {
                if (!counts.ContainsKey(g.Type)) { counts[g.Type] = 0; order.Add(g.Type); }
                counts[g.Type] += g.Count;
            }
            var parts = new List<string>();
            foreach (var t in order)
            {
                var def = EnemyData.Get(t);
                parts.Add(counts[t] + " " + (counts[t] == 1 ? def.Name : def.ShortName));
            }
            return string.Join(", ", parts.ToArray());
        }
    }

    /// <summary>A playable map: road, hazards, waves.</summary>
    public class LevelDef
    {
        public int Id;
        public string Name, Subtitle;
        public Vector2[] Path;
        public Vector2 HeroStart, HeroRespawn, Gate;
        public Vector2[] Vents = new Vector2[0];
        public bool HasRockslide;
        public Vector2 CliffPosition, SlideFrom, SlideTo;
        public bool HasAshStorm;
        public WaveDef[] Waves;
        public bool IsTutorial;
        public LevelTheme Theme = LevelTheme.Ashen;
        public int StartGold = Balance.StartGold;
        public Vector2 TutorialTowerSpot, TutorialHeroSpot;

        /// <summary>The final boss wave (for the F10 debug skip).</summary>
        public int BossWave
        {
            get
            {
                for (int i = Waves.Length - 1; i >= 0; i--) if (Waves[i].HasBoss) return i + 1;
                return Waves.Length;
            }
        }
    }

    public static class Levels
    {
        public const int TutorialId = 0;
        public const int AshenPassId = 1;
        public const int WeepingMarshId = 2;

        static SpawnGroup G(EnemyType t, int count, float interval, float delay = 0f) => new SpawnGroup(t, count, interval, delay);
        static WaveDef W(params SpawnGroup[] g) => new WaveDef(g);

        const EnemyType Gr = EnemyType.Grunt, Wo = EnemyType.WolfRider, Dr = EnemyType.WarDrummer, Ir = EnemyType.Ironhide,
            Go = EnemyType.GoblinSneak, Be = EnemyType.Berserker,
            Sk = EnemyType.Skeleton, Gh = EnemyType.Ghoul, Wr = EnemyType.Wraith, Ba = EnemyType.Banshee,
            Ne = EnemyType.Necromancer, Bg = EnemyType.BoneGolem;

        public static readonly LevelDef Tutorial = new LevelDef
        {
            Id = TutorialId,
            Name = "Training Grounds",
            Subtitle = "Learn to hold the line",
            Path = new[]
            {
                new Vector2(-18f, 1f), new Vector2(-9f, 1f), new Vector2(-9f, 6f), new Vector2(2f, 6f),
                new Vector2(2f, -3f), new Vector2(17f, -3f)
            },
            HeroStart = new Vector2(10f, -3f),
            HeroRespawn = new Vector2(15.5f, -3f),
            Gate = new Vector2(17f, -3f),
            IsTutorial = true,
            StartGold = 400,
            TutorialTowerSpot = new Vector2(-6f, 3.5f),
            TutorialHeroSpot = new Vector2(2f, 1.5f),
            Waves = new[]
            {
                W(G(Gr, 4, 1.8f)),
                W(G(Gr, 7, 1.2f)),
                W(G(Wo, 4, 1.2f), G(Gr, 5, 1.2f, 3f)),
                W(G(Go, 3, 1.6f), G(Gr, 6, 1.1f, 2f)),
                W(G(Gr, 8, 0.9f), G(Dr, 1, 1f, 3f)),
                W(G(EnemyType.Brakka, 1, 1f, 2f), G(Gr, 8, 1f))
            }
        };

        public static readonly LevelDef AshenPass = new LevelDef
        {
            Id = AshenPassId,
            Name = "The Ashen Pass",
            Subtitle = "A scorched mountain road on the eastern frontier",
            Path = new[]
            {
                new Vector2(-18f, 4f), new Vector2(-10f, 4f), new Vector2(-10f, -4f), new Vector2(-2f, -4f),
                new Vector2(-2f, 5f), new Vector2(6f, 5f), new Vector2(6f, -3f), new Vector2(13f, -3f),
                new Vector2(13f, 1f), new Vector2(17f, 1f)
            },
            Vents = new[] { new Vector2(-2f, 0.5f) },
            HasRockslide = true,
            CliffPosition = new Vector2(6f, 9.3f),
            SlideFrom = new Vector2(6f, 8.4f),
            SlideTo = new Vector2(6f, -6.5f),
            HasAshStorm = true,
            HeroStart = new Vector2(9.5f, -3f),
            HeroRespawn = new Vector2(15.5f, 1f),
            Gate = new Vector2(17f, 1f),
            Waves = new[]
            {
                // 1-10: the Warhost arrives. Wolf Riders (4), Ironhides (7), Warchief Brakka (10).
                W(G(Gr, 6, 1.4f)),
                W(G(Gr, 10, 1.1f)),
                W(G(Gr, 12, 0.9f)),
                W(G(Wo, 5, 1.0f), G(Gr, 8, 1.0f, 3f)),
                W(G(Gr, 14, 0.8f), G(Wo, 6, 1.0f, 6f)),
                W(G(Wo, 10, 0.7f), G(Gr, 10, 0.8f, 4f)),
                W(G(Ir, 4, 1.6f), G(Gr, 12, 0.8f, 3f)),
                W(G(Gr, 16, 0.7f), G(Ir, 5, 1.5f, 5f), G(Wo, 6, 0.8f, 10f)),
                W(G(Wo, 14, 0.55f), G(Ir, 6, 1.2f, 6f), G(Gr, 10, 0.8f, 8f)),
                W(G(EnemyType.Brakka, 1, 1f, 4f), G(Gr, 14, 0.8f), G(Ir, 4, 1.5f, 8f)),
                // 11-20: War Drummers (11), Goblin Sneaks (14), Berserkers (18), Troll Juggernaut (20).
                W(G(Dr, 1, 1f), G(Gr, 14, 0.7f, 2f)),
                W(G(Dr, 2, 5f), G(Ir, 6, 1.2f, 2f), G(Wo, 10, 0.6f, 8f)),
                W(G(Gr, 20, 0.5f), G(Dr, 2, 6f, 3f), G(Wo, 8, 0.7f, 10f)),
                W(G(Go, 6, 1.0f), G(Gr, 10, 0.8f, 4f)),
                W(G(Go, 8, 0.9f), G(Ir, 8, 1.0f, 3f), G(Dr, 1, 1f, 8f)),
                W(G(Wo, 16, 0.45f), G(Go, 8, 0.8f, 6f), G(Dr, 2, 5f, 4f)),
                W(G(Gr, 24, 0.45f), G(Ir, 8, 1.0f, 4f), G(Go, 10, 0.7f, 10f)),
                W(G(Be, 3, 3f), G(Gr, 12, 0.7f, 2f)),
                W(G(Be, 5, 2.4f), G(Go, 10, 0.7f, 4f), G(Dr, 2, 5f, 6f)),
                W(G(EnemyType.Troll, 1, 1f, 4f), G(Gr, 18, 0.6f), G(Be, 3, 3f, 10f)),
                // 21-30: everything at once, ending with Gorruk Skullsplitter.
                W(G(Ir, 12, 0.9f), G(Dr, 3, 4f, 3f), G(Go, 10, 0.6f, 8f)),
                W(G(Wo, 20, 0.4f), G(Be, 5, 2f, 5f), G(Go, 12, 0.6f, 8f)),
                W(G(Gr, 30, 0.35f), G(Ir, 10, 0.9f, 4f), G(Dr, 3, 4f, 6f)),
                W(G(Be, 8, 1.6f), G(Go, 14, 0.5f, 4f), G(Wo, 12, 0.5f, 10f)),
                W(G(Ir, 16, 0.7f), G(Dr, 4, 4f, 2f), G(Be, 6, 2f, 10f)),
                W(G(Go, 20, 0.4f), G(Wo, 20, 0.4f, 6f), G(Dr, 3, 4f, 4f)),
                W(G(Gr, 36, 0.3f), G(Be, 10, 1.4f, 6f), G(Ir, 12, 0.8f, 10f)),
                W(G(Wo, 28, 0.3f), G(Go, 18, 0.45f, 5f), G(Dr, 4, 3f, 4f)),
                W(G(Ir, 18, 0.6f), G(Be, 12, 1.2f, 4f), G(Go, 16, 0.5f, 8f), G(Dr, 5, 3f, 6f)),
                W(G(EnemyType.Gorruk, 1, 1f, 4f), G(Gr, 20, 0.6f), G(Wo, 12, 0.8f, 10f), G(Dr, 2, 5f, 6f), G(Be, 6, 2f, 16f))
            }
        };

        public static readonly LevelDef WeepingMarsh = new LevelDef
        {
            Id = WeepingMarshId,
            Name = "The Weeping Marsh",
            Subtitle = "Fog-drowned graveyards in the west",
            Theme = LevelTheme.Marsh,
            Path = new[]
            {
                new Vector2(-18f, -7f), new Vector2(-12f, -7f), new Vector2(-12f, 6f), new Vector2(-5f, 6f),
                new Vector2(-5f, -3f), new Vector2(1f, -3f), new Vector2(1f, 7f), new Vector2(8f, 7f),
                new Vector2(8f, -6f), new Vector2(13f, -6f), new Vector2(13f, -1f), new Vector2(17f, -1f)
            },
            Vents = new[] { new Vector2(-5f, 1.5f), new Vector2(8f, 0.5f) },
            HasRockslide = true,
            CliffPosition = new Vector2(1f, 9.3f),
            SlideFrom = new Vector2(1f, 8.4f),
            SlideTo = new Vector2(1f, -5.8f),
            HasAshStorm = true,
            HeroStart = new Vector2(8f, -2f),
            HeroRespawn = new Vector2(15.5f, -1f),
            Gate = new Vector2(17f, -1f),
            Waves = new[]
            {
                // 1-10: the dead rise. Ghouls (4), Wraiths (7), Banshees (9), Death Knight Varn (10).
                W(G(Sk, 8, 1.2f)),
                W(G(Sk, 12, 0.9f)),
                W(G(Sk, 16, 0.75f)),
                W(G(Gh, 6, 1.0f), G(Sk, 8, 0.9f, 3f)),
                W(G(Sk, 14, 0.7f), G(Gh, 8, 0.8f, 5f)),
                W(G(Gh, 12, 0.6f), G(Sk, 12, 0.7f, 4f)),
                W(G(Wr, 5, 1.4f), G(Sk, 12, 0.8f, 2f)),
                W(G(Sk, 18, 0.6f), G(Wr, 6, 1.2f, 5f), G(Gh, 8, 0.7f, 8f)),
                W(G(Ba, 1, 1f), G(Sk, 14, 0.7f, 2f), G(Gh, 8, 0.7f, 8f)),
                W(G(EnemyType.DeathKnight, 1, 1f, 4f), G(Sk, 16, 0.7f), G(Wr, 4, 1.4f, 8f)),
                // 11-20: Necromancers (12), Bone Golems (16), Plague Abomination (20).
                W(G(Ba, 2, 6f), G(Gh, 14, 0.5f, 3f), G(Wr, 6, 1.0f, 8f)),
                W(G(Ne, 1, 1f), G(Sk, 14, 0.7f, 2f)),
                W(G(Ne, 2, 6f), G(Wr, 8, 0.9f, 3f), G(Gh, 12, 0.5f, 8f)),
                W(G(Sk, 28, 0.4f), G(Ba, 2, 6f, 4f), G(Ne, 1, 1f, 10f)),
                W(G(Wr, 12, 0.7f), G(Gh, 16, 0.45f, 4f), G(Ba, 2, 5f, 6f)),
                W(G(Bg, 2, 4f), G(Sk, 16, 0.6f, 2f)),
                W(G(Bg, 3, 3.5f), G(Ne, 2, 6f, 4f), G(Wr, 10, 0.7f, 6f)),
                W(G(Gh, 24, 0.35f), G(Bg, 2, 4f, 6f), G(Ba, 3, 4f, 4f)),
                W(G(Sk, 30, 0.35f), G(Ne, 3, 5f, 3f), G(Wr, 12, 0.6f, 10f)),
                W(G(EnemyType.Abomination, 1, 1f, 4f), G(Gh, 16, 0.5f), G(Bg, 2, 5f, 10f)),
                // 21-30: the full Legion, ending with Vesperine, the Lich Queen.
                W(G(Bg, 5, 2.5f), G(Ba, 3, 4f, 3f), G(Wr, 14, 0.5f, 8f)),
                W(G(Ne, 4, 4f), G(Gh, 24, 0.35f, 4f), G(Sk, 20, 0.45f, 8f)),
                W(G(Wr, 20, 0.4f), G(Bg, 4, 3f, 5f), G(Ba, 4, 4f, 4f)),
                W(G(Sk, 40, 0.25f), G(Ne, 4, 4f, 4f), G(Gh, 16, 0.4f, 12f)),
                W(G(Bg, 7, 2f), G(Wr, 16, 0.45f, 4f), G(Ba, 4, 3.5f, 6f)),
                W(G(Gh, 32, 0.28f), G(Ne, 5, 3.5f, 4f), G(Wr, 14, 0.5f, 10f)),
                W(G(Bg, 8, 1.8f), G(Sk, 30, 0.3f, 4f), G(Ba, 5, 3f, 6f)),
                W(G(Wr, 26, 0.32f), G(Gh, 24, 0.3f, 6f), G(Ne, 5, 3f, 4f)),
                W(G(Bg, 9, 1.6f), G(Ba, 6, 3f, 4f), G(Wr, 18, 0.4f, 8f), G(Ne, 4, 4f, 6f)),
                W(G(EnemyType.Vesperine, 1, 1f, 4f), G(Sk, 24, 0.5f), G(Wr, 12, 0.7f, 10f), G(Ba, 3, 5f, 8f), G(Bg, 4, 3f, 16f))
            }
        };

        public static LevelDef Get(int id) => id == TutorialId ? Tutorial : id == WeepingMarshId ? WeepingMarsh : AshenPass;
    }
}

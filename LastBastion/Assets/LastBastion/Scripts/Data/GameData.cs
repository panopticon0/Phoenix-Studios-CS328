using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    public enum EnemyType { Grunt, WolfRider, WarDrummer, Gorruk }
    public enum TowerType { Archer, Catapult, Mage }
    public enum DamageKind { Physical, Magic, True }

    /// <summary>Level-wide tuning numbers. Change these to rebalance the alpha.</summary>
    public static class Balance
    {
        public const int StartGold = 300;
        public const float FirstWaveDelay = 30f;   // seconds before wave 1 starts on its own
        public const float WaveBreak = 15f;        // countdown between waves
        public const int EarlyBonusPerSecond = 3;  // gold per second skipped when calling a wave early
        public const float SellRefund = 0.7f;
        public const float DrummerAuraRadius = 3.5f;
        public const float DrummerTowerSlow = 0.7f; // towers attack at 70% speed (30% slower)
        public const float WarCrySpeed = 1.5f;      // Gorruk's War Cry: +50% speed

        // Free tower placement
        public const float TowerFootprint = 0.68f;  // tower radius; towers must be 2x this apart
        public const float MinRoadDistance = 1.5f;  // keeps towers off the road (road half-width 0.85)
        public const float MaxRoadDistance = 4.5f;  // towers must be built "along the path"

        public static int WaveBounty(int waveNumber) => 10 + 5 * waveNumber;
    }

    public class EnemyDef
    {
        public string Name, ShortName, Intro;
        public float MaxHp, Speed, Armor, HeroDps, Radius;
        public int Gold, LifeCost;
        public Color Body;
    }

    public static class EnemyData
    {
        static readonly EnemyDef grunt = new EnemyDef
        {
            Name = "Orc Grunt", ShortName = "Grunts",
            Intro = "Basic infantry in large numbers.",
            MaxHp = 60f, Speed = 1.6f, Armor = 0f, HeroDps = 8f, Radius = 0.32f, Gold = 6, LifeCost = 1,
            Body = Palette.OrcArmor
        };

        static readonly EnemyDef wolf = new EnemyDef
        {
            Name = "Wolf Rider", ShortName = "Wolf Riders",
            Intro = "Wolf Riders move twice as fast as Grunts. Block them with Sir Aldric, or slow them with Tar Pots and Frost Bolts.",
            MaxHp = 45f, Speed = 3.2f, Armor = 0f, HeroDps = 10f, Radius = 0.38f, Gold = 8, LifeCost = 1,
            Body = Palette.Hex("#7A746C")
        };

        static readonly EnemyDef drummer = new EnemyDef
        {
            Name = "War Drummer", ShortName = "War Drummers",
            Intro = "War Drummer! Its drums slow every tower within range by 30%. Kill it first, or hit it with a Mage Tower's Dispel to silence it.",
            MaxHp = 130f, Speed = 1.3f, Armor = 0.1f, HeroDps = 6f, Radius = 0.42f, Gold = 15, LifeCost = 2,
            Body = Palette.Hex("#8A3A20")
        };

        static readonly EnemyDef gorruk = new EnemyDef
        {
            Name = "Gorruk Skullsplitter", ShortName = "Gorruk",
            Intro = "Gorruk Skullsplitter, Orc Warlord! He hurls his axe to knock a tower out for 5 seconds, and at half health his War Cry drives every orc 50% faster.",
            MaxHp = 1800f, Speed = 1.0f, Armor = 0.3f, HeroDps = 35f, Radius = 0.8f, Gold = 150, LifeCost = 10,
            Body = Palette.Hex("#7A2E1A")
        };

        public static EnemyDef Get(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.WolfRider: return wolf;
                case EnemyType.WarDrummer: return drummer;
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
        public string Name, Role;
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
                { new UpgradeDef("Eagle Eye", "+20% range. Sees invisible enemies."),
                  new UpgradeDef("Hunter's Mark", "Hit enemies take +15% damage from everything for 4s."),
                  new UpgradeDef("Flame Arrows", "Arrows set enemies on fire (6 dmg/s for 3s).") }
            }
        };

        static readonly TowerDef catapult = new TowerDef
        {
            Type = TowerType.Catapult, Name = "Catapult", Role = "Heavy splash, slow",
            Cost = 175, Damage = 34f, Interval = 3.0f, Range = 5.5f, Splash = 1.3f, Kind = DamageKind.Physical,
            Roof = Palette.Hex("#8A5A32"),
            Upgrades = new UpgradeDef[,]
            {
                { new UpgradeDef("Heavy Stones", "+25% damage."),
                  new UpgradeDef("Boulders", "Splash radius +40%."),
                  new UpgradeDef("Earthshaker", "Impacts stun enemies for 1s.") },
                { new UpgradeDef("Counterweights", "+20% attack speed."),
                  new UpgradeDef("Twin Arms", "+40% attack speed."),
                  new UpgradeDef("Siege Engine", "Fires 3 stones per volley.") },
                { new UpgradeDef("Tar Pots", "Impacts slow enemies by 30% for 2s."),
                  new UpgradeDef("Firepots", "Impacts burn enemies (8 dmg/s for 3s)."),
                  new UpgradeDef("Spotter Crow", "+30% range.") }
            }
        };

        static readonly TowerDef mage = new TowerDef
        {
            Type = TowerType.Mage, Name = "Mage Tower", Role = "Magic ignores armor",
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
                  new UpgradeDef("True Sight", "+20% range. Sees invisible enemies."),
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

    /// <summary>Level 1, The Ashen Pass: layout and waves.</summary>
    public static class Level1Data
    {
        public const string Name = "The Ashen Pass";

        // Enemy road, from the mountain cave (left) to the Bastion gate (right).
        public static readonly Vector2[] Path =
        {
            new Vector2(-18f, 4f), new Vector2(-10f, 4f), new Vector2(-10f, -4f), new Vector2(-2f, -4f),
            new Vector2(-2f, 5f), new Vector2(6f, 5f), new Vector2(6f, -3f), new Vector2(13f, -3f),
            new Vector2(13f, 1f), new Vector2(17f, 1f)
        };

        public static readonly Vector2 VentPosition = new Vector2(-2f, 0.5f);
        public static readonly Vector2 HeroStart = new Vector2(9.5f, -3f);
        public static readonly Vector2 HeroRespawn = new Vector2(15.5f, 1f);
        public static readonly Vector2 GatePosition = new Vector2(17f, 1f);

        // Alpha pacing: 10 waves. New enemy types debut early so a booth demo sees everything.
        public static readonly WaveDef[] Waves =
        {
            new WaveDef(new SpawnGroup(EnemyType.Grunt, 6, 1.4f)),
            new WaveDef(new SpawnGroup(EnemyType.Grunt, 10, 1.1f)),
            new WaveDef(new SpawnGroup(EnemyType.WolfRider, 5, 1.0f), new SpawnGroup(EnemyType.Grunt, 6, 1.3f, 3f)),
            new WaveDef(new SpawnGroup(EnemyType.Grunt, 12, 0.9f), new SpawnGroup(EnemyType.WolfRider, 6, 1.2f, 6f)),
            new WaveDef(new SpawnGroup(EnemyType.Grunt, 10, 1.0f), new SpawnGroup(EnemyType.WarDrummer, 1, 1f, 4f)),
            new WaveDef(new SpawnGroup(EnemyType.Grunt, 14, 0.8f), new SpawnGroup(EnemyType.WolfRider, 8, 0.9f, 5f)),
            new WaveDef(new SpawnGroup(EnemyType.WarDrummer, 2, 6f), new SpawnGroup(EnemyType.Grunt, 14, 0.8f, 2f), new SpawnGroup(EnemyType.WolfRider, 6, 1f, 10f)),
            new WaveDef(new SpawnGroup(EnemyType.WolfRider, 14, 0.6f), new SpawnGroup(EnemyType.WarDrummer, 1, 1f, 3f)),
            new WaveDef(new SpawnGroup(EnemyType.Grunt, 20, 0.6f), new SpawnGroup(EnemyType.WarDrummer, 3, 5f, 2f), new SpawnGroup(EnemyType.WolfRider, 10, 0.8f, 8f)),
            new WaveDef(new SpawnGroup(EnemyType.Gorruk, 1, 1f, 4f), new SpawnGroup(EnemyType.Grunt, 10, 1f), new SpawnGroup(EnemyType.WolfRider, 6, 1.5f, 10f), new SpawnGroup(EnemyType.WarDrummer, 1, 1f, 14f))
        };
    }
}

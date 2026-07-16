using UnityEngine;

namespace DeckBattle.Tests
{
    internal static class TestDefinitions
    {
        public static BattleConfig CreateConfig()
        {
            BattleConfig config = ScriptableObject.CreateInstance<BattleConfig>();
            config.StartingPlayerHp = 30;
            config.StartingEnemyHp = 30;
            config.StartingAp = 3;
            config.MaxAp = 8;
            config.StartingHandSize = 3;
            config.DrawPerRound = 2;
            config.StartingDeploymentSlots = 3;
            config.MaxDeploymentSlots = 7;
            config.DeploymentSlotIncreaseEveryRounds = 2;
            config.BoardWidth = 5;
            config.BoardHeight = 6;
            config.PreparationCountdownSeconds = 10f;
            return config;
        }

        public static UnitDefinition CreateUnit(string unitId, int apCost)
        {
            return CreateUnit(unitId, apCost, UnitType.Melee);
        }

        public static UnitDefinition CreateUnit(string unitId, int apCost, UnitType unitType)
        {
            UnitDefinition unit = ScriptableObject.CreateInstance<UnitDefinition>();
            unit.UnitId = unitId;
            unit.DisplayName = unitId;
            unit.UnitType = unitType;
            unit.Rarity = UnitRarity.Common;
            unit.ApCost = apCost;
            unit.MaxHp = 5;
            unit.Attack = 2;
            unit.Power = 2;
            unit.AttackRange = 1;
            unit.CritChance = 0f;
            unit.CritMultiplier = 2f;
            unit.AttackCooldown = 1f;
            unit.ManaThreshold = 100;
            unit.ManaPerAttack = 10;
            unit.ManaPerDamageTaken = 10;
            unit.Armor = 0f;
            unit.ArmorPenetration = 0f;
            return unit;
        }
    }
}

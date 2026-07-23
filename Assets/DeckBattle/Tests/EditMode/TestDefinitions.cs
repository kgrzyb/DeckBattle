using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle.Tests
{
    internal static class TestDefinitions
    {
        private static readonly List<Object> CreatedObjects = new List<Object>(64);

        public static BattleConfig CreateConfig()
        {
            BattleConfig config = Track(ScriptableObject.CreateInstance<BattleConfig>());
            config.StartingPlayerHp = 30;
            config.StartingEnemyHp = 30;
            config.StartingAp = 3;
            config.ApIncreasePerStep = 1;
            config.ApIncreaseEveryRounds = 1;
            config.MaxAp = 8;
            config.StartingHandSize = 3;
            config.MaxHandSize = 7;
            config.DrawPerRound = 2;
            config.StartingDeploymentSlots = 3;
            config.DeploymentSlotIncreasePerStep = 1;
            config.MaxDeploymentSlots = 7;
            config.DeploymentSlotIncreaseEveryRounds = 2;
            config.StartingRoundDamageBonus = 0;
            config.RoundDamageBonusIncreasePerStep = 0;
            config.RoundDamageBonusIncreaseEveryRounds = 1;
            config.MaxRoundDamageBonus = 0;
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
            UnitDefinition unit = Track(ScriptableObject.CreateInstance<UnitDefinition>());
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

        public static SpellDefinition CreateSpell(
            string cardId,
            int apCost,
            SpellEffectKind effectKind = SpellEffectKind.BuffAttackNextCombat,
            SpellTargetingKind targetingKind = SpellTargetingKind.FriendlyUnit,
            int amount = 1)
        {
            SpellDefinition spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            spell.CardId = cardId;
            spell.DisplayName = cardId;
            spell.Rarity = UnitRarity.Common;
            spell.ApCost = apCost;
            spell.EffectKind = effectKind;
            spell.TargetingKind = targetingKind;
            spell.Amount = amount;
            return spell;
        }

        public static CardCatalog CreateCatalog(
            IReadOnlyList<CardDefinition> allCards,
            IReadOnlyList<CardDefinition> startingCards,
            IReadOnlyList<CardDefinition> defaultDeckCards)
        {
            CardCatalog catalog = Track(ScriptableObject.CreateInstance<CardCatalog>());
            catalog.Configure(allCards, startingCards, defaultDeckCards);
            return catalog;
        }

        public static T Track<T>(T createdObject) where T : Object
        {
            if (createdObject != null)
            {
                CreatedObjects.Add(createdObject);
            }

            return createdObject;
        }

        public static void DestroyCreatedObjects()
        {
            for (int i = CreatedObjects.Count - 1; i >= 0; i--)
            {
                Object createdObject = CreatedObjects[i];
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            CreatedObjects.Clear();
        }
    }
}

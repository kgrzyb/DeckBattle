using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class UnitOnPlayEffectTests
    {
        private const string SharedEffectAssetPath = "Assets/DeckBattle/Data/UnitEffects/OnPlay_BaseAttack25_NextCombat.asset";

        [Test]
        public void Content_AllProductionUnitsUseTheSharedDirectBaseAttackModifier()
        {
            UnitOnPlayEffectDefinition sharedEffect = AssetDatabase.LoadAssetAtPath<UnitOnPlayEffectDefinition>(SharedEffectAssetPath);
            Assert.IsNotNull(sharedEffect);
            Assert.AreEqual(1, sharedEffect.Steps.Length);
            Assert.AreEqual(EffectTargetKind.Self, sharedEffect.Steps[0].Target.Kind);
            Assert.AreEqual(CombatEffectKind.ModifyBaseAttackPercent, sharedEffect.Steps[0].Effect.Kind);
            Assert.AreEqual(0.25f, sharedEffect.Steps[0].Effect.Percent, 0.0001f);

            string[] unitGuids = AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/DeckBattle/Data/Units" });
            Assert.AreEqual(9, unitGuids.Length);
            for (int i = 0; i < unitGuids.Length; i++)
            {
                UnitDefinition unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(unitGuids[i]));
                Assert.AreSame(sharedEffect, unit.OnPlayEffect, unit.DisplayName);
            }
        }

        [Test]
        public void PlayUnit_SelfEffectQueuesOneDirectModifierForThePlayedUnit()
        {
            BattleState state = CreateState(32);
            UnitDefinition definition = TestDefinitions.CreateUnit("focused", 1);
            definition.OnPlayEffect = CreateBaseAttackEffect();
            CardRuntimeState card = AddPlayerUnitCard(state, definition);

            PlayUnitResult result = UnitPlayService.PlayUnit(state, state.Player, card, new HexCoord(0, 0));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.QueuedCombatEffectCount);
            Assert.AreEqual(1, state.PendingCombatEffects.Count);
            PendingCombatEffect effect = state.PendingCombatEffects[0];
            Assert.AreEqual(state.RoundNumber, effect.ScheduledRoundNumber);
            Assert.AreEqual(result.Unit.RuntimeId, effect.SourceRuntimeUnitId);
            Assert.AreEqual(result.Unit.RuntimeId, effect.TargetRuntimeUnitId);
            Assert.AreEqual(CombatEffectKind.ModifyBaseAttackPercent, effect.Spec.Kind);
            Assert.AreEqual(0.25f, effect.Spec.Percent, 0.0001f);
        }

        [Test]
        public void CreateSimulation_MaterializesOnPlayModifierBeforeCombatAndBoostsOnlyBaseAttack()
        {
            BattleState state = CreateState(32);
            UnitDefinition attackerDefinition = TestDefinitions.CreateUnit("focused", 1);
            attackerDefinition.Attack = 110;
            attackerDefinition.OnPlayEffect = CreateBaseAttackEffect();
            CardRuntimeState attackerCard = AddPlayerUnitCard(state, attackerDefinition);
            PlayUnitResult playResult = UnitPlayService.PlayUnit(state, state.Player, attackerCard, new HexCoord(0, 0));

            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("target", 1);
            targetDefinition.MaxHp = 500;
            var targetRuntime = new RuntimeUnit(state.AllocateRuntimeUnitId(), targetDefinition, BattleSide.Enemy, new HexCoord(0, 5));
            state.Enemy.Units.Add(targetRuntime);

            BattleSimulation simulation = BattleSimulationFactory.Create(state);
            Assert.IsTrue(simulation.TryGetUnitById(playResult.Unit.RuntimeId, out UnitRuntimeState attacker));
            Assert.IsTrue(simulation.TryGetUnitById(targetRuntime.RuntimeId, out UnitRuntimeState target));
            Assert.AreEqual(0, attacker.Statuses.Count);
            Assert.AreEqual(0.25f, attacker.BaseAttackBonusPercent, 0.0001f);

            int damage = DamageCalculator.CalculateDamage(attacker, target, 10, simulation.Tuning, simulation.Random, out bool isCritical);

            Assert.IsFalse(isCritical);
            Assert.AreEqual(148, damage);
        }

        [Test]
        public void PlayUnit_RejectsEffectWhenThePendingQueueCannotFitItsWholeBatch()
        {
            BattleState state = CreateState(1);
            UnitDefinition definition = TestDefinitions.CreateUnit("over-capacity", 1);
            UnitOnPlayEffectDefinition effect = CreateBaseAttackEffect();
            effect.Steps = new[]
            {
                effect.Steps[0],
                effect.Steps[0]
            };
            definition.OnPlayEffect = effect;
            CardRuntimeState card = AddPlayerUnitCard(state, definition);

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, card, new HexCoord(0, 0));

            Assert.AreEqual(PlayUnitFailReason.OnPlayEffectCapacityReached, reason);
            Assert.AreEqual(0, state.PendingCombatEffects.Count);
            Assert.AreEqual(CardLocation.Hand, card.Location);
        }

        [Test]
        public void ResultApplication_ConsumesOnPlayEffectSoItDoesNotReturnInTheNextRound()
        {
            BattleState state = CreateState(32);
            UnitDefinition definition = TestDefinitions.CreateUnit("focused", 1);
            definition.OnPlayEffect = CreateBaseAttackEffect();
            CardRuntimeState card = AddPlayerUnitCard(state, definition);
            PlayUnitResult result = UnitPlayService.PlayUnit(state, state.Player, card, new HexCoord(0, 0));

            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("target", 1);
            state.Enemy.Units.Add(new RuntimeUnit(state.AllocateRuntimeUnitId(), targetDefinition, BattleSide.Enemy, new HexCoord(0, 5)));
            BattleSimulation firstSimulation = BattleSimulationFactory.Create(state);
            BattleSimulationResultApplier.Apply(state, firstSimulation);

            Assert.AreEqual(0, state.PendingCombatEffects.Count);

            state.Phase = BattlePhase.RoundResolution;
            state.StartNextRound();
            BattleSimulation secondSimulation = BattleSimulationFactory.Create(state);

            Assert.IsTrue(secondSimulation.TryGetUnitById(result.Unit.RuntimeId, out UnitRuntimeState unitInSecondCombat));
            Assert.AreEqual(0, unitInSecondCombat.Statuses.Count);
            Assert.AreEqual(0f, unitInSecondCombat.BaseAttackBonusPercent, 0.0001f);
        }

        private static BattleState CreateState(int maxPendingCombatEffects)
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.MaxPendingCombatEffects = maxPendingCombatEffects;
            return BattleState.Create(
                config,
                new CardDefinition[] { TestDefinitions.CreateUnit("player-deck", 1) },
                new CardDefinition[] { TestDefinitions.CreateUnit("enemy-deck", 1) },
                42);
        }

        private static CardRuntimeState AddPlayerUnitCard(BattleState state, UnitDefinition definition)
        {
            var card = new CardRuntimeState(1000 + state.Player.Hand.Count, definition);
            card.Location = CardLocation.Hand;
            state.Player.Hand.Add(card);
            return card;
        }

        private static UnitOnPlayEffectDefinition CreateBaseAttackEffect()
        {
            UnitOnPlayEffectDefinition effect = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitOnPlayEffectDefinition>());
            effect.EffectId = "on-play-base-attack";
            effect.Steps = new[]
            {
                new UnitEffectStepDefinition
                {
                    Target = new EffectTargetDefinition { Kind = EffectTargetKind.Self },
                    Effect = new CombatEffectDefinition
                    {
                        Kind = CombatEffectKind.ModifyBaseAttackPercent,
                        Percent = 0.25f
                    }
                }
            };
            return effect;
        }
    }
}

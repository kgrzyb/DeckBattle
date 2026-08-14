using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class DamageResolverTests
    {
        [TearDown] public void TearDown() { TestDefinitions.DestroyCreatedObjects(); }

        [Test]
        public void ShieldAbsorbsDamageBeforeHpAndSleepRemains()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            Apply(simulation, target, StatusKind.Shield, StatusCategory.Beneficial, 3f);
            Apply(simulation, target, StatusKind.Sleep, StatusCategory.HarmfulCrowdControl, 0f);
            var events = new BattleEventQueue();

            HitResolutionResult result = DamageResolver.Resolve(simulation, target, new DamageRequest(simulation.Units[0], 3), events);

            Assert.AreEqual(0, result.Damage);
            Assert.AreEqual(target.CombatSpec.MaxHp, target.CurrentHp);
            Assert.AreEqual(0, target.CurrentMana);
            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(StatusKind.Sleep, target.Statuses[0].Kind);
            AssertEventTypeDoesNotExist(events, BattleEventType.UnitDamaged);
        }

        [Test]
        public void DamageEvent_CarriesCriticalFlagForAppliedHpDamage()
        {
            BattleSimulation simulation = CreateSimulation();
            var events = new BattleEventQueue();

            DamageResolver.Resolve(simulation, simulation.Units[1], new DamageRequest(simulation.Units[0], 3), events);
            BattleEvent normalDamage = FindEvent(events, BattleEventType.UnitDamaged);
            Assert.AreEqual(3, normalDamage.Amount);
            Assert.IsFalse(normalDamage.IsCritical);

            events.Clear();
            DamageResolver.Resolve(simulation, simulation.Units[1], new DamageRequest(simulation.Units[0], 3, isCritical: true), events);
            BattleEvent criticalDamage = FindEvent(events, BattleEventType.UnitDamaged);
            Assert.AreEqual(3, criticalDamage.Amount);
            Assert.IsTrue(criticalDamage.IsCritical);
        }

        [Test]
        public void SpecialDamage_GrantsManaPulseOnlyToTheDamagedTarget()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            var events = new BattleEventQueue();

            DamageResolver.Resolve(
                simulation,
                target,
                new DamageRequest(attacker, 2, DamageKind.Special, false),
                events);

            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(20, target.CurrentMana);
            Assert.AreEqual(BattleEventType.UnitDamaged, events[0].Type);
            Assert.AreEqual(BattleEventType.UnitManaChanged, events[1].Type);
            Assert.AreEqual(target.UnitId, events[1].UnitId);
        }

        [Test]
        public void ExecuteAfterDamageCrossesThreshold_KillsForRemainingHp()
        {
            UnitDefinition attackerDefinition = TestDefinitions.CreateUnit("execute-attacker", 1);
            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("execute-target", 1);
            targetDefinition.MaxHp = 100;
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(3, 3, 1f),
                new[]
                {
                    new UnitSpawnData(1, attackerDefinition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, targetDefinition, BattleSide.Enemy, new HexCoord(1, 0))
                });
            UnitRuntimeState target = simulation.Units[1];
            target.CurrentHp = 20;
            var events = new BattleEventQueue();

            HitResolutionResult result = DamageResolver.Resolve(
                simulation,
                target,
                new DamageRequest(
                    simulation.Units[0],
                    1,
                    DamageKind.Special,
                    executeHpThresholdPercent: 20),
                events);

            Assert.IsTrue(result.Died);
            Assert.AreEqual(20, result.Damage);
            Assert.AreEqual(0, target.CurrentHp);
            Assert.AreEqual(20, FindEvent(events, BattleEventType.UnitDamaged).Amount);
            AssertEventTypeExists(events, BattleEventType.UnitDied);
        }

        [Test]
        public void ExecuteAfterDamageAtExactThreshold_UsesNormalDamage()
        {
            UnitDefinition attackerDefinition = TestDefinitions.CreateUnit("threshold-attacker", 1);
            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("threshold-target", 1);
            targetDefinition.MaxHp = 100;
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(3, 3, 1f),
                new[]
                {
                    new UnitSpawnData(1, attackerDefinition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, targetDefinition, BattleSide.Enemy, new HexCoord(1, 0))
                });
            UnitRuntimeState target = simulation.Units[1];
            target.CurrentHp = 21;

            HitResolutionResult result = DamageResolver.Resolve(
                simulation,
                target,
                new DamageRequest(
                    simulation.Units[0],
                    1,
                    DamageKind.Special,
                    executeHpThresholdPercent: 20),
                null);

            Assert.IsFalse(result.Died);
            Assert.AreEqual(1, result.Damage);
            Assert.AreEqual(20, target.CurrentHp);
        }

        [Test]
        public void ExecuteAfterShieldAbsorbsDamage_DoesNotTrigger()
        {
            UnitDefinition attackerDefinition = TestDefinitions.CreateUnit("shielded-execute-attacker", 1);
            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("shielded-execute-target", 1);
            targetDefinition.MaxHp = 100;
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(3, 3, 1f),
                new[]
                {
                    new UnitSpawnData(1, attackerDefinition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, targetDefinition, BattleSide.Enemy, new HexCoord(1, 0))
                });
            UnitRuntimeState target = simulation.Units[1];
            target.CurrentHp = 21;
            Apply(simulation, target, StatusKind.Shield, StatusCategory.Beneficial, 2f);

            HitResolutionResult result = DamageResolver.Resolve(
                simulation,
                target,
                new DamageRequest(
                    simulation.Units[0],
                    2,
                    DamageKind.Special,
                    executeHpThresholdPercent: 20),
                null);

            Assert.IsFalse(result.Died);
            Assert.AreEqual(0, result.Damage);
            Assert.AreEqual(21, target.CurrentHp);
        }

        [Test]
        public void DrainRemovesManaWithoutCreatingAStatus()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            target.CurrentMana = 8;
            StatusDefinition drain = Definition(StatusKind.Drain, StatusCategory.HarmfulStatReduction, 5f);
            drain.StackingRule = StatusStackingRule.InstantOnly;

            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(drain, 1)));
            Assert.AreEqual(3, target.CurrentMana);
            Assert.AreEqual(0, target.Statuses.Count);
        }

        private static void Apply(BattleSimulation simulation, UnitRuntimeState target, StatusKind kind, StatusCategory category, float magnitude)
        {
            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(Definition(kind, category, magnitude), 1)));
        }

        private static StatusDefinition Definition(StatusKind kind, StatusCategory category, float magnitude)
        {
            StatusDefinition definition = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            definition.Kind = kind; definition.Category = category; definition.DefaultDuration = 5f; definition.DefaultMagnitude = magnitude; definition.StackingRule = StatusStackingRule.RefreshPerSource;
            return definition;
        }

        private static BattleSimulation CreateSimulation()
        {
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            UnitDefinition second = TestDefinitions.CreateUnit("second", 1);
            return BattleSimulation.Create(new HexBoard(3, 3, 1f), new[] { new UnitSpawnData(1, first, BattleSide.Player, new HexCoord(0, 0)), new UnitSpawnData(2, second, BattleSide.Enemy, new HexCoord(1, 0)) });
        }

        private static BattleEvent FindEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected event type was not emitted: " + type);
            return default;
        }

        private static void AssertEventTypeDoesNotExist(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreNotEqual(type, events[i].Type, "Event type should not have been emitted: " + type);
            }
        }

        private static void AssertEventTypeExists(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return;
                }
            }

            Assert.Fail("Expected event type: " + type);
        }
    }
}

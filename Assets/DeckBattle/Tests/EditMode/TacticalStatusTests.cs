using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class TacticalStatusTests
    {
        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Taunt_SelectsReachableLinkedUnitOverCloserEnemy()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState provocateur = simulation.Units[2];
            Apply(simulation, attacker, StatusKind.Taunt, StatusCategory.HarmfulTactical, 0f, provocateur.UnitId);

            UnitRuntimeState selected = TargetSelector.SelectTarget(simulation, attacker);

            Assert.AreSame(provocateur, selected);
        }

        [Test]
        public void Taunt_IgnoresUntargetableLinkedUnit()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState provocateur = simulation.Units[2];
            Apply(simulation, attacker, StatusKind.Taunt, StatusCategory.HarmfulTactical, 0f, provocateur.UnitId);
            Apply(simulation, provocateur, StatusKind.Untargetable, StatusCategory.Beneficial, 0f, 0);

            UnitRuntimeState selected = TargetSelector.SelectTarget(simulation, attacker);

            Assert.AreSame(simulation.Units[1], selected);
        }

        [Test]
        public void Guard_RedirectsHalfBeforeEachRecipientsDefenses()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            UnitRuntimeState guard = simulation.Units[2];
            Apply(simulation, target, StatusKind.Guard, StatusCategory.Beneficial, 0f, guard.UnitId);

            HitResolutionResult result = DamageResolver.Resolve(simulation, target, new DamageRequest(simulation.Units[0], 5), null);

            Assert.AreEqual(5, result.Damage);
            Assert.AreEqual(target.CombatSpec.MaxHp - 3, target.CurrentHp);
            Assert.AreEqual(guard.CombatSpec.MaxHp - 2, guard.CurrentHp);
        }

        [Test]
        public void Guard_DoesNotRedirectAgainThroughGuardedGuardian()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            UnitRuntimeState firstGuard = simulation.Units[2];
            Apply(simulation, target, StatusKind.Guard, StatusCategory.Beneficial, 0f, firstGuard.UnitId);
            Apply(simulation, firstGuard, StatusKind.Guard, StatusCategory.Beneficial, 0f, target.UnitId);

            DamageResolver.Resolve(simulation, target, new DamageRequest(simulation.Units[0], 4), null);

            Assert.AreEqual(target.CombatSpec.MaxHp - 2, target.CurrentHp);
            Assert.AreEqual(firstGuard.CombatSpec.MaxHp - 2, firstGuard.CurrentHp);
        }

        [Test]
        public void Guard_CriticalDamageMarksBothAppliedDamageEventsWithoutDuplicatingCritEvent()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            UnitRuntimeState guard = simulation.Units[2];
            Apply(simulation, target, StatusKind.Guard, StatusCategory.Beneficial, 0f, guard.UnitId);
            var events = new BattleEventQueue();

            DamageResolver.Resolve(simulation, target, new DamageRequest(simulation.Units[0], 5, isCritical: true), events);

            int criticalEventCount = 0;
            int criticalDamageEventCount = 0;
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.UnitCrit)
                {
                    criticalEventCount++;
                }

                if (battleEvent.Type == BattleEventType.UnitDamaged)
                {
                    Assert.IsTrue(battleEvent.IsCritical);
                    criticalDamageEventCount++;
                }
            }

            Assert.AreEqual(1, criticalEventCount);
            Assert.AreEqual(2, criticalDamageEventCount);
        }

        private static BattleSimulation CreateSimulation()
        {
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            UnitDefinition second = TestDefinitions.CreateUnit("second", 1);
            UnitDefinition third = TestDefinitions.CreateUnit("third", 1);
            return BattleSimulation.Create(new HexBoard(5, 3, 1f), new[]
            {
                new UnitSpawnData(1, first, BattleSide.Player, new HexCoord(0, 0)),
                new UnitSpawnData(2, second, BattleSide.Enemy, new HexCoord(1, 0)),
                new UnitSpawnData(3, third, BattleSide.Enemy, new HexCoord(4, 0))
            });
        }

        private static void Apply(BattleSimulation simulation, UnitRuntimeState target, StatusKind kind, StatusCategory category, float magnitude, int linkedUnitId)
        {
            StatusDefinition definition = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            definition.Kind = kind;
            definition.Category = category;
            definition.DefaultDuration = 5f;
            definition.DefaultMagnitude = magnitude;
            definition.StackingRule = StatusStackingRule.RefreshPerSource;
            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(definition, 99, linkedUnitId: linkedUnitId)));
        }
    }
}

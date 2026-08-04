using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class SpecialCycleResolverTests
    {
        private const float TickDuration = 0.25f;

        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Tick_ReadySpecialStartsWindupWithoutApplyingEffectOrSpendingMana()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Windup, unit.SpecialPhase);
            Assert.That(unit.SpecialWindupEndTime, Is.EqualTo(0.75d).Within(0.000001d));
            Assert.AreEqual(unit.CombatSpec.ManaThreshold, unit.CurrentMana);
            Assert.IsFalse(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialWindupStarted);
        }

        [Test]
        public void Tick_SpecialWindupIgnoresHasteAndCompletesAtConfiguredDeadline()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(CreateHasteStatus(5f, 0.5f), unit.UnitId));
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            AssertEvent(events, BattleEventType.UnitSpecialActivated);
        }

        [Test]
        public void Tick_SpecialCastSpendsManaAtCastStartAndLocksManaUntilRecoveryEnds()
        {
            BattleSimulation simulation = CreateSimulation(0.25f, 0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Casting, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(0, unit.CurrentMana);

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.That(unit.NextAttackTime, Is.EqualTo(6d).Within(0.000001d));
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(0, unit.CurrentMana);

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(10, unit.CurrentMana);
        }

        [Test]
        public void StatusStun_CancelsCastAfterManaWasSpent()
        {
            BattleSimulation simulation = CreateSimulation(0.25f, 1f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());
            var events = new BattleEventQueue();

            StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(CreateStunStatus(), 0),
                events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            Assert.IsFalse(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialWindupCancelled);
        }

        [Test]
        public void Tick_StartedAttackFiresBeforeItsReadySpecialBegins()
        {
            BattleSimulation simulation = CreateSimulation(0.25f);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.NextAttackTime = 0d;
            attacker.SetTarget(simulation.Units[1]);
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);

            AssertEvent(events, BattleEventType.AttackFired);
            Assert.AreEqual(attacker.CombatSpec.ManaThreshold, attacker.CurrentMana);
            Assert.AreEqual(UnitSpecialPhase.Windup, attacker.SpecialPhase);
            AssertEvent(events, BattleEventType.SpecialWindupStarted);
        }

        [Test]
        public void StatusStun_CancelsSpecialWindupWithoutSpendingMana()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            loop.Tick(new BattleEventQueue());
            var events = new BattleEventQueue();

            StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(CreateStunStatus(), 0),
                events);

            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);
            Assert.AreEqual(unit.CombatSpec.ManaThreshold, unit.CurrentMana);
            AssertEvent(events, BattleEventType.SpecialWindupCancelled);
        }

        [Test]
        public void Tick_ReadySpecialBlocksNewAttackWindup()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.NextAttackTime = 0d;
            attacker.SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            new BattleTickLoop(simulation, TickDuration).Tick(events);

            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            Assert.AreEqual(UnitSpecialPhase.Windup, attacker.SpecialPhase);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);
            AssertEvent(events, BattleEventType.SpecialWindupStarted);
        }

        [Test]
        public void Tick_ReadySpecialWaitsForActiveMovementThenStartsBeforeAnotherStep()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            simulation.StartUnitMovement(unit, new HexCoord(0, 1));
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());

            Assert.IsTrue(unit.IsMoving);
            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);

            loop.Tick(new BattleEventQueue());

            Assert.IsFalse(unit.IsMoving);
            Assert.AreEqual(UnitSpecialPhase.Windup, unit.SpecialPhase);
        }

        private static BattleSimulation CreateSimulation(float windupDuration, float castDuration = 0f)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("attacker", 1);
            attacker.AttacksPerSecond = 0.1f;
            attacker.ManaThreshold = 10;
            attacker.Special = CreateHasteBurstSpecial(windupDuration, castDuration);
            UnitDefinition target = TestDefinitions.CreateUnit("target", 1);
            target.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                });
        }

        private static UnitSpecialDefinition CreateHasteBurstSpecial(float windupDuration, float castDuration = 0f)
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.HasteBurst;
            special.WindupDuration = windupDuration;
            special.CastDuration = castDuration;
            special.AppliedStatus = CreateHasteStatus(5f, 0.5f);
            return special;
        }

        private static StatusDefinition CreateHasteStatus(float duration, float magnitude)
        {
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;
            status.Category = StatusCategory.Beneficial;
            status.DefaultDuration = duration;
            status.DefaultMagnitude = magnitude;
            return status;
        }

        private static StatusDefinition CreateStunStatus()
        {
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Stun;
            status.Category = StatusCategory.HarmfulCrowdControl;
            status.DefaultDuration = 1f;
            return status;
        }

        private static void AssertEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type) return;
            }

            Assert.Fail("Expected event: " + type);
        }

        private static void AssertNoEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreNotEqual(type, events[i].Type);
            }
        }
    }
}

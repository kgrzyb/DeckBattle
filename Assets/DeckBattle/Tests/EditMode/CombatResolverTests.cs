using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class CombatResolverTests
    {
        [Test]
        public void GrantManaPulse_AppliesConfiguredManaPerTickOnlyWhileIdle()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            UnitRuntimeState unit = simulation.Units[0];
            var events = new BattleEventQueue();

            unit.SpecialPhase = UnitSpecialPhase.Casting;
            CombatResolver.GrantManaPulse(simulation, unit, events);
            unit.SpecialPhase = UnitSpecialPhase.RecoveryLock;
            CombatResolver.GrantManaPulse(simulation, unit, events);

            Assert.AreEqual(0, unit.CurrentMana);
            Assert.AreEqual(0, events.Count);

            unit.SpecialPhase = UnitSpecialPhase.Idle;
            CombatResolver.GrantManaPulse(simulation, unit, events);

            Assert.AreEqual(3, unit.CurrentMana);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(BattleEventType.UnitManaChanged, events[0].Type);
        }

        [Test]
        public void GrantManaPulse_DoesNotEmitEventWhenManaIsAlreadyAtThreshold()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var events = new BattleEventQueue();

            CombatResolver.GrantManaPulse(simulation, unit, events);

            Assert.AreEqual(unit.CombatSpec.ManaThreshold, unit.CurrentMana);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Tick_WaitsForInitialCooldownThenSchedulesFromWindupStart()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            BattleTickResult first = loop.Tick(events);
            BattleTickResult second = loop.Tick(events);
            BattleTickResult third = loop.Tick(events);
            BattleTickResult fourth = loop.Tick(events);
            BattleTickResult fifth = loop.Tick(events);

            Assert.AreEqual(0, first.Attacks);
            Assert.AreEqual(0, second.Attacks);
            Assert.AreEqual(1, third.Attacks);
            Assert.AreEqual(0, fourth.Attacks);
            Assert.AreEqual(1, fifth.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1.9d).Within(0.000001d));
        }

        [Test]
        public void Tick_SchedulesCooldownPointSevenFromActualWindupStart()
        {
            BattleSimulation simulation = CreateSimulation(0.7f);
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            BattleTickResult first = loop.Tick(events);
            BattleTickResult second = loop.Tick(events);
            BattleTickResult third = loop.Tick(events);
            BattleTickResult fourth = loop.Tick(events);
            BattleTickResult fifth = loop.Tick(events);

            Assert.AreEqual(0, first.Attacks);
            Assert.AreEqual(0, second.Attacks);
            Assert.AreEqual(1, third.Attacks);
            Assert.AreEqual(0, fourth.Attacks);
            Assert.AreEqual(1, fifth.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(2.1d).Within(0.000001d));
        }

        [Test]
        public void Tick_CommitsAttackersThatFinishWindupTogether()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 5, 1, 1f);
            UnitDefinition target = CreateUnit("target", 3, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, attacker, BattleSide.Player, new HexCoord(2, 0)),
                    new UnitSpawnData(3, target, BattleSide.Enemy, new HexCoord(2, 1))
                });
            simulation.Units[0].SetTarget(simulation.Units[2]);
            simulation.Units[1].SetTarget(simulation.Units[2]);
            simulation.Units[0].NextAttackTime = 0d;
            simulation.Units[1].NextAttackTime = 0d;

            BattleTickResult result = TestDefinitions.ResolveNextAttack(simulation);

            Assert.AreEqual(2, result.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1.25d).Within(0.000001d));
            Assert.That(simulation.Units[1].NextAttackTime, Is.EqualTo(1.25d).Within(0.000001d));
        }

        [Test]
        public void Tick_HasteBurstActivatesAfterSpecialCastStartsAndAppliesHasteForFollowingCycle()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            attacker.ManaThreshold = 10;
            attacker.Special = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            attacker.Special.Kind = UnitSpecialKind.HasteBurst;
            attacker.Special.AppliedStatus = CreateHasteStatus(5f, 0.5f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, CreateUnit("target", 10, 1, 1, 1f), BattleSide.Enemy, new HexCoord(2, 1))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;
            var events = new BattleEventQueue();

            TestDefinitions.ResolveNextAttack(simulation, events);

            Assert.IsFalse(simulation.Units[0].Statuses.TryFind(StatusKind.Haste, simulation.Units[0].UnitId, out _));
            Assert.AreEqual(9, simulation.Units[0].CurrentMana);
            ResolveReadySpecial(simulation, events);

            Assert.IsTrue(simulation.Units[0].Statuses.TryFind(StatusKind.Haste, simulation.Units[0].UnitId, out int hasteIndex));
            Assert.That(simulation.Units[0].Statuses[hasteIndex].EndTime, Is.EqualTo(6d).Within(0.000001d));
            Assert.AreEqual(0.5f, simulation.Units[0].StatusSnapshot.Haste);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1.25d).Within(0.000001d));
            AssertSpecialActivation(events, UnitSpecialKind.HasteBurst);
        }

        [Test]
        public void Tick_ExpiresHasteAppliedBySpecialWithoutReschedulingAttack()
        {
            UnitDefinition attacker = CreateUnit("attacker", 100, 1, 1, 1f);
            attacker.Special = CreateHasteBurstSpecial();
            BattleSimulation simulation = CreateSimulation(attacker, CreateUnit("target", 100, 1, 1, 1f));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            StatusResolver.TryApply(
                simulation,
                simulation.Units[0],
                new StatusApplicationRequest(attacker.Special.AppliedStatus, simulation.Units[0].UnitId));
            simulation.Units[0].NextAttackTime = 10d;
            var loop = new BattleTickLoop(simulation, 5f);

            loop.Tick(new BattleEventQueue());

            Assert.IsFalse(simulation.Units[0].Statuses.TryFind(StatusKind.Haste, 0, out _));
            Assert.AreEqual(0f, simulation.Units[0].StatusSnapshot.Haste);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(10d).Within(0.000001d));
        }

        [Test]
        public void Tick_ReactivatingHasteBurstRefreshesHasteDuration()
        {
            UnitDefinition attacker = CreateUnit("attacker", 100, 1, 1, 1f);
            attacker.ManaThreshold = 10;
            attacker.Special = CreateHasteBurstSpecial();
            BattleSimulation simulation = CreateSimulation(attacker, CreateUnit("target", 100, 1, 1, 1f));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;

            TestDefinitions.ResolveNextAttack(simulation);
            ResolveReadySpecial(simulation, new BattleEventQueue());
            AssertHasteEndTime(simulation.Units[0], 6d);

            TestDefinitions.ResolveNextAttack(simulation);
            ResolveReadySpecial(simulation, new BattleEventQueue());

            AssertHasteEndTime(simulation.Units[0], 7.25d);
        }

        [Test]
        public void Tick_UnitWithoutSpecialDoesNotGainHaste()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            attacker.ManaThreshold = 10;
            BattleSimulation simulation = CreateSimulation(attacker, CreateUnit("target", 10, 1, 1, 1f));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;
            var events = new BattleEventQueue();

            TestDefinitions.ResolveNextAttack(simulation, events);

            Assert.IsFalse(simulation.Units[0].Statuses.TryFind(StatusKind.Haste, simulation.Units[0].UnitId, out _));
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1.25d).Within(0.000001d));
            AssertNoSpecialActivation(events);
        }

        [Test]
        public void Tick_MovingUnitWaitsForAcquireReloadBeforeStartingWindup()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            simulation.StartUnitMovement(simulation.Units[0], new HexCoord(2, 0));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            BattleTickResult movingTick = loop.Tick(events);

            Assert.AreEqual(0, movingTick.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(0d).Within(0.000001d));
        }

        private static BattleSimulation CreateSimulation(float cooldown)
        {
            UnitDefinition target = CreateUnit("target", 50, 1, 1, 999f);
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, CreateUnit("attacker", 50, 1, 1, cooldown), BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                });
        }

        private static BattleSimulation CreateSimulation(UnitDefinition attacker, UnitDefinition target)
        {
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                });
        }

        private static UnitSpecialDefinition CreateHasteBurstSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.HasteBurst;
            special.AppliedStatus = CreateHasteStatus(5f, 0.5f);
            return special;
        }

        private static StatusDefinition CreateHasteStatus(float duration, float magnitude)
        {
            StatusDefinition status = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;
            status.Category = StatusCategory.Beneficial;
            status.DefaultDuration = duration;
            status.DefaultMagnitude = magnitude;
            return status;
        }

        private static void AssertHasteEndTime(UnitRuntimeState unit, double expectedEndTime)
        {
            Assert.IsTrue(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out int hasteIndex));
            Assert.That(unit.Statuses[hasteIndex].EndTime, Is.EqualTo(expectedEndTime).Within(0.000001d));
        }

        private static void ResolveReadySpecial(BattleSimulation simulation, BattleEventQueue events)
        {
            var loop = new BattleTickLoop(simulation, 0.25f);
            for (int i = 0; i < 64; i++)
            {
                loop.Tick(events);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    if (events[eventIndex].Type == BattleEventType.SpecialCastCompleted)
                    {
                        return;
                    }
                }
            }

            Assert.Fail("Special did not activate within the test tick budget.");
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int range, float cooldown)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = range;
            definition.AttacksPerSecond = 1f / cooldown;
            return definition;
        }

        private static void AssertSpecialActivation(BattleEventQueue events, UnitSpecialKind kind)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.SpecialCastCompleted && battleEvent.SpecialKind == kind)
                {
                    return;
                }
            }

            Assert.Fail("Expected special activation was not emitted.");
        }

        private static void AssertNoSpecialActivation(BattleEventQueue events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreNotEqual(BattleEventType.SpecialCastCompleted, events[i].Type);
            }
        }
    }
}

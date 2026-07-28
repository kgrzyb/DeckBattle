using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class AttackCycleResolverTests
    {
        private const float TickDuration = 0.25f;

        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Tick_WaitsFullInitialCooldownBeforeStartingWindup()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(simulation, events);
            loop.Tick(simulation, events);
            loop.Tick(simulation, events);

            Assert.AreEqual(UnitAttackPhase.AcquireReload, simulation.Units[0].AttackPhase);
            Assert.AreEqual(5, simulation.Units[1].CurrentHp);

            BattleTickResult windupTick = loop.Tick(simulation, events);

            Assert.AreEqual(0, windupTick.Attacks);
            Assert.AreEqual(UnitAttackPhase.Windup, simulation.Units[0].AttackPhase);
            Assert.That(simulation.Units[0].AttackCycleStartTime, Is.EqualTo(1d).Within(0.000001d));
            Assert.That(simulation.Units[0].WindupEndTime, Is.EqualTo(1.25d).Within(0.000001d));
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(2d).Within(0.000001d));
            AssertEvent(events, BattleEventType.AttackWindupStarted);

            BattleTickResult fireTick = loop.Tick(simulation, events);

            Assert.AreEqual(1, fireTick.Attacks);
            Assert.AreEqual(UnitAttackPhase.Winddown, simulation.Units[0].AttackPhase);
            Assert.AreEqual(4, simulation.Units[1].CurrentHp);
            Assert.AreEqual(simulation.Units[1].UnitId, simulation.Units[0].EngagedTargetUnitId);
            Assert.AreEqual(0, simulation.Units[0].PursuitStepsUsed);
            Assert.That(FindEvent(events, BattleEventType.AttackFired).Duration, Is.EqualTo(0.75f).Within(0.000001f));
        }

        [Test]
        public void Haste_ShortensWindupAndWholeCycleFromWindupStart()
        {
            BattleSimulation normal = CreateSimulation(4f);
            BattleSimulation accelerated = CreateSimulation(4f);
            normal.Units[0].NextAttackTime = 0d;
            accelerated.Units[0].NextAttackTime = 0d;
            StatusResolver.TryApply(
                accelerated,
                accelerated.Units[0],
                new StatusApplicationRequest(CreateHasteStatus(0.5f), accelerated.Units[0].UnitId));
            var normalLoop = new BattleTickLoop(normal, TickDuration);
            var acceleratedLoop = new BattleTickLoop(accelerated, TickDuration);

            normalLoop.Tick(normal, new BattleEventQueue());
            acceleratedLoop.Tick(accelerated, new BattleEventQueue());

            Assert.That(normal.Units[0].WindupEndTime - normal.Units[0].AttackCycleStartTime, Is.EqualTo(1d).Within(0.000001d));
            Assert.That(normal.Units[0].NextAttackTime - normal.Units[0].AttackCycleStartTime, Is.EqualTo(4d).Within(0.000001d));
            Assert.That(accelerated.Units[0].WindupEndTime - accelerated.Units[0].AttackCycleStartTime, Is.EqualTo(0.5d).Within(0.000001d));
            Assert.That(accelerated.Units[0].NextAttackTime - accelerated.Units[0].AttackCycleStartTime, Is.EqualTo(2d).Within(0.000001d));
        }

        [Test]
        public void Windup_CannotBeShorterThanOneTick()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            simulation.Units[0].NextAttackTime = 0d;
            StatusResolver.TryApply(
                simulation,
                simulation.Units[0],
                new StatusApplicationRequest(CreateHasteStatus(0.9f), simulation.Units[0].UnitId));
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(simulation, new BattleEventQueue());

            UnitRuntimeState attacker = simulation.Units[0];
            Assert.That(attacker.WindupEndTime - attacker.AttackCycleStartTime, Is.EqualTo(TickDuration).Within(0.000001d));
            Assert.That(attacker.NextAttackTime, Is.EqualTo(attacker.WindupEndTime).Within(0.000001d));
        }

        [Test]
        public void MovementStep_BlocksWindupUntilItsFullDurationEnds()
        {
            UnitDefinition attackerDefinition = TestDefinitions.CreateUnit("moving-attacker", 1);
            attackerDefinition.AttackCooldown = 1f;
            attackerDefinition.AttackWindupPercent = 0.25f;

            UnitDefinition targetDefinition = TestDefinitions.CreateUnit("stationary-target", 1);
            targetDefinition.AttackRange = 3;
            targetDefinition.AttackCooldown = 999f;

            var tuning = new BattleRuntimeTuning(1f, 0, 0.4f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attackerDefinition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, targetDefinition, BattleSide.Enemy, new HexCoord(2, 0))
                },
                tuning);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.NextAttackTime = 0d;
            simulation.Units[1].NextAttackTime = 999d;
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            loop.Tick(simulation, events);

            Assert.AreEqual(new HexCoord(0, 0), attacker.CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), attacker.MovementDestination);
            Assert.IsTrue(attacker.IsMoving);
            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);

            loop.Tick(simulation, events);

            Assert.IsTrue(attacker.IsMoving);
            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);

            loop.Tick(simulation, events);

            Assert.IsFalse(attacker.IsMoving);
            Assert.AreEqual(UnitAttackPhase.Windup, attacker.AttackPhase);
            AssertEvent(events, BattleEventType.AttackWindupStarted);
        }

        [Test]
        public void MovementDetectedDuringWindup_CancelsAttackWithoutFiring()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();
            loop.Tick(simulation, events);
            Assert.AreEqual(UnitAttackPhase.Windup, attacker.AttackPhase);

            attacker.IsMoving = true;
            attacker.MovementDestination = attacker.CurrentHex;
            attacker.MovementTimeRemaining = 1f;

            BattleTickResult result = loop.Tick(simulation, events);

            Assert.AreEqual(0, result.Attacks);
            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            Assert.AreEqual(5, simulation.Units[1].CurrentHp);
            AssertEvent(events, BattleEventType.AttackWindupCancelled);
            AssertNoEvent(events, BattleEventType.AttackFired);
        }

        [Test]
        public void StartUnitMovement_DuringWindupIsRejected()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, TickDuration);
            loop.Tick(simulation, new BattleEventQueue());

            Assert.AreEqual(UnitAttackPhase.Windup, attacker.AttackPhase);
            Assert.Throws<System.InvalidOperationException>(
                () => simulation.StartUnitMovement(attacker, new HexCoord(1, 2)));
            Assert.IsFalse(attacker.IsMoving);
        }

        [Test]
        public void ResetDuringWinddown_EndsOnlyRemainingWinddown()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(simulation, events);
            loop.Tick(simulation, events);
            UnitRuntimeState attacker = simulation.Units[0];
            Assert.AreEqual(UnitAttackPhase.Winddown, attacker.AttackPhase);

            AttackResetResult result = AttackCycleResolver.TryResetWinddown(simulation, attacker, events);

            Assert.AreEqual(AttackResetResult.Applied, result);
            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            Assert.That(attacker.NextAttackTime, Is.EqualTo(simulation.ElapsedTime).Within(0.000001d));
            AssertEvent(events, BattleEventType.AttackWinddownEnded);

            BattleTickResult nextTick = loop.Tick(simulation, events);
            Assert.AreEqual(0, nextTick.Attacks);
            Assert.AreEqual(UnitAttackPhase.Windup, attacker.AttackPhase);
        }

        [Test]
        public void ResetDuringWindup_IsIgnoredWithoutChangingCommittedAttack()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, TickDuration);
            loop.Tick(simulation, new BattleEventQueue());
            UnitRuntimeState attacker = simulation.Units[0];
            double windupEndTime = attacker.WindupEndTime;
            double nextAttackTime = attacker.NextAttackTime;
            int sequenceId = attacker.AttackSequenceId;

            AttackResetResult result = AttackCycleResolver.TryResetWinddown(simulation, attacker);

            Assert.AreEqual(AttackResetResult.IgnoredDuringWindup, result);
            Assert.AreEqual(UnitAttackPhase.Windup, attacker.AttackPhase);
            Assert.AreEqual(sequenceId, attacker.AttackSequenceId);
            Assert.That(attacker.WindupEndTime, Is.EqualTo(windupEndTime).Within(0.000001d));
            Assert.That(attacker.NextAttackTime, Is.EqualTo(nextAttackTime).Within(0.000001d));
        }

        [Test]
        public void DeadLockedTarget_CancelsWindupWithoutFiring()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();
            loop.Tick(simulation, events);

            simulation.DefeatUnit(simulation.Units[1]);
            BattleTickResult result = loop.Tick(simulation, events);

            Assert.AreEqual(0, result.Attacks);
            Assert.AreEqual(UnitAttackPhase.AcquireReload, simulation.Units[0].AttackPhase);
            AssertEvent(events, BattleEventType.AttackWindupCancelled);
            AssertNoEvent(events, BattleEventType.AttackFired);
        }

        private static BattleSimulation CreateSimulation(float cooldown)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("attacker", 1);
            attacker.Attack = 1;
            attacker.AttackCooldown = cooldown;
            attacker.AttackWindupPercent = 0.25f;

            UnitDefinition target = TestDefinitions.CreateUnit("target", 1);
            target.MaxHp = 5;
            target.Attack = 0;
            target.AttackCooldown = 999f;

            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                });
        }

        private static StatusDefinition CreateHasteStatus(float magnitude)
        {
            StatusDefinition status = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;
            status.Category = StatusCategory.Beneficial;
            status.DefaultDuration = 10f;
            status.DefaultMagnitude = magnitude;
            return status;
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

            Assert.Fail("Expected event " + type + ".");
            return default;
        }

        private static void AssertEvent(BattleEventQueue events, BattleEventType type)
        {
            FindEvent(events, type);
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

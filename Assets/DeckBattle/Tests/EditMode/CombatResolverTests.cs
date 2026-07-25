using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class CombatResolverTests
    {
        [Test]
        public void Tick_WaitsForInitialCooldownThenSchedulesFromPreviousDeadline()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            BattleTickResult first = loop.Tick(simulation, events);
            BattleTickResult second = loop.Tick(simulation, events);
            BattleTickResult third = loop.Tick(simulation, events);

            Assert.AreEqual(0, first.Attacks);
            Assert.AreEqual(1, second.Attacks);
            Assert.AreEqual(1, third.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1.5d).Within(0.000001d));
        }

        [Test]
        public void Tick_PreservesOvershootForCooldownPointSeven()
        {
            BattleSimulation simulation = CreateSimulation(0.7f);
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            BattleTickResult first = loop.Tick(simulation, events);
            BattleTickResult second = loop.Tick(simulation, events);
            BattleTickResult third = loop.Tick(simulation, events);
            BattleTickResult fourth = loop.Tick(simulation, events);

            Assert.AreEqual(0, first.Attacks);
            Assert.AreEqual(1, second.Attacks);
            Assert.AreEqual(0, third.Attacks);
            Assert.AreEqual(1, fourth.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(2.1d).Within(0.000001d));
        }

        [Test]
        public void ResolveCombat_OnlySchedulesAttackThatActuallyExecutes()
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

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation);

            Assert.AreEqual(1, result.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1d).Within(0.000001d));
            Assert.That(simulation.Units[1].NextAttackTime, Is.EqualTo(0d).Within(0.000001d));
        }

        [Test]
        public void ResolveCombat_ActivatesAssignedAttackSpeedSpecialBeforeSchedulingNextAttack()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            attacker.ManaThreshold = 10;
            attacker.Special = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            attacker.Special.Kind = UnitSpecialKind.AttackSpeed;
            attacker.Special.Duration = 5f;
            attacker.Special.AttackCooldownMultiplier = 0.5f;
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

            CombatResolver.ResolveCombat(simulation, events);

            Assert.AreSame(attacker.Special, simulation.Units[0].ActiveSpecial);
            Assert.That(simulation.Units[0].SpecialEndTime, Is.EqualTo(5d).Within(0.000001d));
            Assert.AreEqual(0.5f, simulation.Units[0].SpecialAttackCooldownMultiplier);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(0.5d).Within(0.000001d));
            AssertSpecialActivation(events, UnitSpecialKind.AttackSpeed);
        }

        [Test]
        public void Tick_ExpiresAttackSpeedSpecialAtItsAbsoluteEndTimeWithoutReschedulingAttack()
        {
            UnitDefinition attacker = CreateUnit("attacker", 100, 1, 1, 1f);
            attacker.Special = CreateAttackSpeedSpecial();
            BattleSimulation simulation = CreateSimulation(attacker, CreateUnit("target", 100, 1, 1, 1f));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].ActiveSpecial = attacker.Special;
            simulation.Units[0].SpecialEndTime = 5d;
            simulation.Units[0].SpecialAttackCooldownMultiplier = 0.5f;
            simulation.Units[0].NextAttackTime = 10d;
            var loop = new BattleTickLoop(simulation, 5f);

            loop.Tick(simulation, new BattleEventQueue());

            Assert.IsNull(simulation.Units[0].ActiveSpecial);
            Assert.IsTrue(double.IsPositiveInfinity(simulation.Units[0].SpecialEndTime));
            Assert.AreEqual(1f, simulation.Units[0].SpecialAttackCooldownMultiplier);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(10d).Within(0.000001d));
        }

        [Test]
        public void Tick_ReactivatingAttackSpeedSpecialRefreshesItsAbsoluteEndTime()
        {
            UnitDefinition attacker = CreateUnit("attacker", 100, 1, 1, 1f);
            attacker.ManaThreshold = 10;
            attacker.Special = CreateAttackSpeedSpecial();
            BattleSimulation simulation = CreateSimulation(attacker, CreateUnit("target", 100, 1, 1, 1f));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;

            CombatResolver.ResolveCombat(simulation);
            Assert.That(simulation.Units[0].SpecialEndTime, Is.EqualTo(5d).Within(0.000001d));

            var loop = new BattleTickLoop(simulation, 1f);
            loop.Tick(simulation, new BattleEventQueue());

            Assert.That(simulation.Units[0].SpecialEndTime, Is.EqualTo(6d).Within(0.000001d));
            Assert.AreSame(attacker.Special, simulation.Units[0].ActiveSpecial);
        }

        [Test]
        public void ResolveCombat_UnitWithoutSpecialDoesNotGainHaste()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            attacker.ManaThreshold = 10;
            BattleSimulation simulation = CreateSimulation(attacker, CreateUnit("target", 10, 1, 1, 1f));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;
            var events = new BattleEventQueue();

            CombatResolver.ResolveCombat(simulation, events);

            Assert.IsNull(simulation.Units[0].ActiveSpecial);
            Assert.AreEqual(1f, simulation.Units[0].SpecialAttackCooldownMultiplier);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1d).Within(0.000001d));
            AssertNoSpecialActivation(events);
        }

        [Test]
        public void Tick_MovingUnitKeepsElapsedAttackDeadlineAndAttacksAfterMovementCompletes()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            simulation.StartUnitMovement(simulation.Units[0], new HexCoord(2, 0));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            BattleTickResult movingTick = loop.Tick(simulation, events);
            BattleTickResult completedTick = loop.Tick(simulation, events);

            Assert.AreEqual(0, movingTick.Attacks);
            Assert.AreEqual(1, completedTick.Attacks);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(1d).Within(0.000001d));
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

        private static UnitSpecialDefinition CreateAttackSpeedSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.AttackSpeed;
            special.Duration = 5f;
            special.AttackCooldownMultiplier = 0.5f;
            return special;
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int range, float cooldown)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = range;
            definition.AttackCooldown = cooldown;
            return definition;
        }

        private static void AssertSpecialActivation(BattleEventQueue events, UnitSpecialKind kind)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.UnitSpecialActivated && battleEvent.SpecialKind == kind)
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
                Assert.AreNotEqual(BattleEventType.UnitSpecialActivated, events[i].Type);
            }
        }
    }
}

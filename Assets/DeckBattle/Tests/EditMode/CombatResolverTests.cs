using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class CombatResolverTests
    {
        [Test]
        public void ResolveCombat_WaitsInitialCooldownBeforeAttacking()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("attacker", 10, 2, 1, 1f),
                new HexCoord(1, 1),
                CreateUnit("target", 10, 1, 1, 1f),
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult beforeCooldown = CombatResolver.ResolveCombat(simulation, 0.25f);

            Assert.AreEqual(0, beforeCooldown.Attacks);
            Assert.AreEqual(0, beforeCooldown.TotalDamage);
            Assert.AreEqual(0, beforeCooldown.Deaths);
            Assert.AreEqual(10, simulation.Units[1].CurrentHp);
            Assert.AreEqual(0.75f, simulation.Units[0].AttackCooldownRemaining);
            Assert.AreEqual(0, simulation.Units[0].CurrentMana);
            Assert.AreEqual(0, simulation.Units[1].CurrentMana);

            CombatResolutionResult afterCooldown = CombatResolver.ResolveCombat(simulation, 0.75f);

            Assert.AreEqual(1, afterCooldown.Attacks);
            Assert.AreEqual(2, afterCooldown.TotalDamage);
            Assert.AreEqual(0, afterCooldown.Deaths);
            Assert.AreEqual(8, simulation.Units[1].CurrentHp);
            Assert.AreEqual(1f, simulation.Units[0].AttackCooldownRemaining);
            Assert.AreEqual(10, simulation.Units[0].CurrentMana);
            Assert.AreEqual(10, simulation.Units[1].CurrentMana);
        }

        [Test]
        public void ResolveCombat_ReducesCooldownAndDoesNotAttackUntilReady()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("attacker", 10, 2, 1, 1f),
                new HexCoord(1, 1),
                CreateUnit("target", 10, 1, 1, 1f),
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].AttackCooldownRemaining = 0.75f;

            CombatResolutionResult first = CombatResolver.ResolveCombat(simulation, 0.25f);

            Assert.AreEqual(0, first.Attacks);
            Assert.AreEqual(0.5f, simulation.Units[0].AttackCooldownRemaining);

            CombatResolutionResult second = CombatResolver.ResolveCombat(simulation, 0.5f);

            Assert.AreEqual(1, second.Attacks);
            Assert.AreEqual(8, simulation.Units[1].CurrentHp);
            Assert.AreEqual(1f, simulation.Units[0].AttackCooldownRemaining);
        }

        [Test]
        public void ResolveCombat_DoesNotAttackTargetOutOfRange()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("attacker", 10, 2, 1, 1f),
                new HexCoord(0, 0),
                CreateUnit("target", 10, 1, 1, 1f),
                new HexCoord(4, 5));
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 1f);

            Assert.AreEqual(0, result.Attacks);
            Assert.AreEqual(10, simulation.Units[1].CurrentHp);
        }

        [Test]
        public void ResolveCombat_DefeatsTargetAndReleasesOccupiedHex()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("attacker", 10, 5, 1, 1f),
                new HexCoord(1, 1),
                CreateUnit("target", 3, 1, 1, 1f),
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 1f);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(1, result.Deaths);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
            Assert.IsFalse(simulation.Units[1].IsAlive);
            Assert.IsFalse(simulation.TryGetUnitAt(new HexCoord(2, 1), out UnitRuntimeState _));
        }

        [Test]
        public void ResolveCombat_DoesNotKeepAttackingDeadTarget()
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

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 1f);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(1, result.Deaths);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, simulation.Units[1].TargetUnitId);
            Assert.IsTrue(simulation.Units[2].IsDefeated);
        }

        [Test]
        public void ResolveCombat_OutOfRangeUnitCanMoveBeforeLaterAttack()
        {
            UnitDefinition melee = CreateUnit("melee", 10, 2, 1, 1f);
            UnitDefinition ranged = CreateUnit("ranged", 10, 1, 5, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, ranged, BattleSide.Enemy, new HexCoord(2, 1))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult combat = CombatResolver.ResolveCombat(simulation, 1f);
            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(0, combat.Attacks);
            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
        }

        [Test]
        public void ResolveCombat_ActivatesSpecialAtManaThresholdAndResetsMana()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            UnitDefinition target = CreateUnit("target", 10, 1, 1, 1f);
            attacker.ManaThreshold = 10;
            BattleSimulation simulation = CreateSimulation(
                attacker,
                new HexCoord(1, 1),
                target,
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            CombatResolver.ResolveCombat(simulation, 1f, events);

            Assert.AreEqual(0, simulation.Units[0].CurrentMana);
            Assert.AreEqual(0.5f, simulation.Units[0].AttackCooldownMultiplier);
            Assert.AreEqual(5f, simulation.Units[0].SpecialDurationRemaining);
            Assert.AreEqual(0.5f, simulation.Units[0].AttackCooldownRemaining);
            AssertEventTypeExists(events, BattleEventType.UnitSpecialActivated);
            AssertManaChangedEventExists(events, 1, 0);
        }

        [Test]
        public void ResolveCombat_EmitsManaChangedAfterAttackManaGain()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            UnitDefinition target = CreateUnit("target", 10, 1, 1, 1f);
            target.ManaPerDamageTaken = 0;
            BattleSimulation simulation = CreateSimulation(
                attacker,
                new HexCoord(1, 1),
                target,
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            CombatResolver.ResolveCombat(simulation, 1f, events);

            AssertManaChangedEventExists(events, 1, 10);
        }

        [Test]
        public void ResolveCombat_EmitsManaChangedAfterDamageTakenManaGain()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10, 2, 1, 1f);
            UnitDefinition target = CreateUnit("target", 10, 1, 1, 1f);
            attacker.ManaPerAttack = 0;
            target.ManaPerDamageTaken = 7;
            BattleSimulation simulation = CreateSimulation(
                attacker,
                new HexCoord(1, 1),
                target,
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            CombatResolver.ResolveCombat(simulation, 1f, events);

            AssertManaChangedEventExists(events, 2, 7);
        }

        [Test]
        public void ResolveCombat_SpecialExpiresAfterFiveSeconds()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("attacker", 10, 2, 1, 1f),
                new HexCoord(1, 1),
                CreateUnit("target", 10, 1, 1, 1f),
                new HexCoord(2, 1));
            simulation.Units[0].SpecialDurationRemaining = 5f;
            simulation.Units[0].AttackCooldownMultiplier = 0.5f;
            simulation.Units[0].AttackCooldownRemaining = 10f;

            CombatResolver.ResolveCombat(simulation, 5f);

            Assert.AreEqual(0f, simulation.Units[0].SpecialDurationRemaining);
            Assert.AreEqual(1f, simulation.Units[0].AttackCooldownMultiplier);
        }

        [Test]
        public void ResolveCombat_MovingUnitCannotAttackButCanBeAttackedOnCommittedHex()
        {
            UnitDefinition melee = CreateUnit("melee", 10, 2, 1, 1f);
            UnitDefinition ranged = CreateUnit("ranged", 10, 3, 2, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, ranged, BattleSide.Enemy, new HexCoord(3, 1))
                });
            simulation.StartUnitMovement(simulation.Units[0], new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[1].SetTarget(simulation.Units[0]);

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 1f);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(7, simulation.Units[0].CurrentHp);
            Assert.AreEqual(10, simulation.Units[1].CurrentHp);
        }

        private static BattleSimulation CreateSimulation(
            UnitDefinition attacker,
            HexCoord attackerHex,
            UnitDefinition target,
            HexCoord targetHex)
        {
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, attackerHex),
                    new UnitSpawnData(2, target, BattleSide.Enemy, targetHex)
                });
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, float attackCooldown)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = attackRange;
            definition.AttackCooldown = attackCooldown;
            return definition;
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

            Assert.Fail("Expected event type was not emitted: " + type);
        }

        private static void AssertManaChangedEventExists(BattleEventQueue events, int unitId, int currentMana)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.UnitManaChanged
                    && battleEvent.UnitId == unitId
                    && battleEvent.CurrentMana == currentMana)
                {
                    return;
                }
            }

            Assert.Fail("Expected mana event was not emitted for unit " + unitId + " with mana " + currentMana + ".");
        }
    }
}

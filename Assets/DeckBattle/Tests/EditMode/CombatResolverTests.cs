using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class CombatResolverTests
    {
        [Test]
        public void ResolveCombat_AttacksLiveTargetInRangeAndResetsCooldown()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("attacker", 10, 2, 1, 1f),
                new HexCoord(1, 1),
                CreateUnit("target", 10, 1, 1, 1f),
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 0.25f);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(2, result.TotalDamage);
            Assert.AreEqual(0, result.Deaths);
            Assert.AreEqual(8, simulation.Units[1].CurrentHp);
            Assert.AreEqual(1f, simulation.Units[0].AttackCooldownRemaining);
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
                    new UnitSpawnData(attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(attacker, BattleSide.Player, new HexCoord(2, 0)),
                    new UnitSpawnData(target, BattleSide.Enemy, new HexCoord(2, 1))
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
                    new UnitSpawnData(melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(ranged, BattleSide.Enemy, new HexCoord(2, 1))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult combat = CombatResolver.ResolveCombat(simulation, 1f);
            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(0, combat.Attacks);
            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].CurrentHex);
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
                    new UnitSpawnData(attacker, BattleSide.Player, attackerHex),
                    new UnitSpawnData(target, BattleSide.Enemy, targetHex)
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
    }
}

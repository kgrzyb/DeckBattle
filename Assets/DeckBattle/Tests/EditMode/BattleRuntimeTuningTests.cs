using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleRuntimeTuningTests
    {
        [Test]
        public void AttackRangeBonus_AllowsUnitToAttackFromTunedRange()
        {
            UnitDefinition player = CreateUnit("player", 5, 5, 1, 1f);
            UnitDefinition enemy = CreateUnit("enemy", 3, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                },
                new BattleRuntimeTuning(1f, 1, 1));
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(simulation, events);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(0, result.Moves);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
        }

        [Test]
        public void AttackCooldownMultiplier_AdjustsCooldownAfterAttack()
        {
            UnitDefinition player = CreateUnit("player", 10, 1, 3, 1f);
            UnitDefinition enemy = CreateUnit("enemy", 10, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                },
                new BattleRuntimeTuning(2f, 0, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolver.ResolveCombat(simulation, 1f);

            Assert.AreEqual(2f, simulation.Units[0].AttackCooldownRemaining);
        }

        [Test]
        public void MovementStepsPerTick_AllowsLongerLogicalMove()
        {
            UnitDefinition player = CreateUnit("player", 5, 1, 1, 1f);
            UnitDefinition enemy = CreateUnit("enemy", 5, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(3, 0))
                },
                new BattleRuntimeTuning(1f, 0, 2));
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(simulation, events);

            Assert.GreaterOrEqual(result.Moves, 1);
            Assert.AreEqual(new HexCoord(2, 0), simulation.Units[0].CurrentHex);
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

using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class TargetSelectorTests
    {
        [Test]
        public void SelectTarget_ReturnsNearestReachableEnemy()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(4, 5)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(1, 1))
                });
            simulation.Units[1].CurrentHp = 1;
            simulation.Units[2].CurrentHp = 5;

            UnitRuntimeState target = TargetSelector.SelectTarget(simulation, simulation.Units[0]);

            Assert.AreSame(simulation.Units[2], target);
        }

        [Test]
        public void SelectTarget_FallsBackToLowestHpReachable_WhenNearestEnemyCannotBeReached()
        {
            var board = new HexBoard(5, 6, 1f);
            BlockAttackRing(board, new HexCoord(2, 0), 1);

            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(2, 0)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(4, 5)),
                    new UnitSpawnData(4, melee, BattleSide.Enemy, new HexCoord(3, 4))
                });
            simulation.Units[1].CurrentHp = 5;
            simulation.Units[2].CurrentHp = 1;
            simulation.Units[3].CurrentHp = 3;

            UnitRuntimeState target = TargetSelector.SelectTarget(simulation, simulation.Units[0]);

            Assert.AreSame(simulation.Units[2], target);
        }

        [Test]
        public void SelectTarget_UsesUnitIdTieBreaker_ForNearestEnemy()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(2, 2)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(3, 2)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(2, 3))
                });

            UnitRuntimeState target = TargetSelector.SelectTarget(simulation, simulation.Units[0]);

            Assert.AreSame(simulation.Units[2], target);
        }

        [Test]
        public void SelectTarget_IgnoresDefeatedEnemies()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(1, 1)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(4, 5))
                });
            simulation.Units[1].IsDefeated = true;

            UnitRuntimeState target = TargetSelector.SelectTarget(simulation, simulation.Units[0]);

            Assert.AreSame(simulation.Units[2], target);
        }

        [Test]
        public void SelectTarget_ReturnsNull_WhenNoReachableEnemyExists()
        {
            var board = new HexBoard(5, 6, 1f);
            BlockAttackRing(board, new HexCoord(2, 0), 1);
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(2, 0))
                });

            UnitRuntimeState target = TargetSelector.SelectTarget(simulation, simulation.Units[0]);

            Assert.IsNull(target);
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attackRange)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.AttackRange = attackRange;
            return definition;
        }

        private static void BlockAttackRing(HexBoard board, HexCoord target, int range)
        {
            var hexes = new List<HexCoord>(8);
            board.FillHexesInRange(target, range, hexes);
            for (int i = 0; i < hexes.Count; i++)
            {
                HexCoord coord = hexes[i];
                if (coord != target)
                {
                    board.SetWalkable(coord, false);
                }
            }
        }
    }
}

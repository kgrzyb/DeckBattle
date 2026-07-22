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
        public void SelectTarget_FallsBackToNearestReachable_WhenNearestEnemyCannotBeReached()
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

            Assert.AreSame(simulation.Units[3], target);
        }

        [Test]
        public void SelectTarget_UsesLowestHpTieBreaker_ForEnemiesAtSameDistance()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(2, 2)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(3, 2)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(2, 3))
                });
            simulation.Units[1].CurrentHp = 5;
            simulation.Units[2].CurrentHp = 1;

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
        public void SelectTargetOrRetainCurrent_ReevaluatesAllPaths_AndChoosesCloserTarget()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(4, 5)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(0, 1))
                });
            simulation.Units[1].CurrentHp = 5;
            simulation.Units[2].CurrentHp = 1;
            simulation.Units[0].SetTarget(simulation.Units[1]);

            UnitRuntimeState target = TargetSelector.SelectTargetOrRetainCurrent(
                simulation,
                simulation.Units[0],
                new TargetSelector.Workspace(board.Width * board.Height));

            Assert.AreSame(simulation.Units[2], target);
        }

        [Test]
        public void SelectTargetOrRetainCurrent_SelectsNewTarget_WhenCurrentTargetCannotBeReached()
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
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(4, 5))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);

            UnitRuntimeState target = TargetSelector.SelectTargetOrRetainCurrent(
                simulation,
                simulation.Units[0],
                new TargetSelector.Workspace(board.Width * board.Height));

            Assert.AreSame(simulation.Units[2], target);
        }

        [Test]
        public void TrySelectTarget_ReturnsSelectedTargetAndItsAttackPath()
        {
            UnitDefinition melee = CreateUnit("melee", 5, 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(3, 0))
                });
            var workspace = new TargetSelector.Workspace(30);

            bool found = TargetSelector.TrySelectTarget(
                simulation,
                simulation.Units[0],
                workspace,
                out TargetSelector.TargetSelection selection);

            Assert.IsTrue(found);
            Assert.AreSame(simulation.Units[1], selection.Target);
            Assert.AreEqual(new HexCoord(1, 0), selection.AttackPath.NextStep);
            Assert.AreEqual(2, selection.AttackPath.PathSteps);
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

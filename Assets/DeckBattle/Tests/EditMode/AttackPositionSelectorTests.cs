using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class AttackPositionSelectorTests
    {
        [Test]
        public void TrySelectAttackPosition_ReturnsCurrentHex_WhenTargetAlreadyInRange()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(melee, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(melee, BattleSide.Enemy, new HexCoord(2, 1))
                });

            bool found = AttackPositionSelector.TrySelectAttackPosition(
                simulation,
                simulation.Units[0],
                simulation.Units[1],
                out HexCoord attackPosition);

            Assert.IsTrue(found);
            Assert.AreEqual(new HexCoord(1, 1), attackPosition);
        }

        [Test]
        public void TrySelectAttackPosition_MeleeSelectsNearestFreeNeighbor()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(melee, BattleSide.Enemy, new HexCoord(2, 1))
                });

            bool found = AttackPositionSelector.TrySelectAttackPosition(
                simulation,
                simulation.Units[0],
                simulation.Units[1],
                out HexCoord attackPosition);

            Assert.IsTrue(found);
            Assert.AreEqual(new HexCoord(1, 1), attackPosition);
        }

        [Test]
        public void TrySelectAttackPosition_RangedSelectsNearestFreeHexInRange()
        {
            UnitDefinition ranged = CreateUnit("ranged", 3);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(ranged, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(ranged, BattleSide.Enemy, new HexCoord(3, 2))
                });

            bool found = AttackPositionSelector.TrySelectAttackPosition(
                simulation,
                simulation.Units[0],
                simulation.Units[1],
                out HexCoord attackPosition);

            Assert.IsTrue(found);
            Assert.AreEqual(new HexCoord(0, 2), attackPosition);
        }

        [Test]
        public void TrySelectAttackPosition_SkipsOccupiedCandidateHexes()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(melee, BattleSide.Enemy, new HexCoord(2, 1)),
                    new UnitSpawnData(melee, BattleSide.Player, new HexCoord(1, 1))
                });

            bool found = AttackPositionSelector.TrySelectAttackPosition(
                simulation,
                simulation.Units[0],
                simulation.Units[1],
                out HexCoord attackPosition);

            Assert.IsTrue(found);
            Assert.AreEqual(new HexCoord(2, 0), attackPosition);
        }

        [Test]
        public void TrySelectAttackPosition_ReturnsFalse_WhenNoAttackHexIsReachable()
        {
            var board = new HexBoard(5, 6, 1f);
            board.SetWalkable(new HexCoord(1, 1), false);
            board.SetWalkable(new HexCoord(1, 2), false);
            board.SetWalkable(new HexCoord(2, 0), false);
            board.SetWalkable(new HexCoord(2, 2), false);
            board.SetWalkable(new HexCoord(3, 0), false);
            board.SetWalkable(new HexCoord(3, 1), false);

            UnitDefinition melee = CreateUnit("melee", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(melee, BattleSide.Enemy, new HexCoord(2, 1))
                });

            bool found = AttackPositionSelector.TrySelectAttackPosition(
                simulation,
                simulation.Units[0],
                simulation.Units[1],
                out HexCoord attackPosition);

            Assert.IsFalse(found);
            Assert.AreEqual(default(HexCoord), attackPosition);
        }

        private static UnitDefinition CreateUnit(string unitId, int attackRange)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.AttackRange = attackRange;
            return definition;
        }
    }
}

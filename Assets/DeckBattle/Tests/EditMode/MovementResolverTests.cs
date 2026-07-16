using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class MovementResolverTests
    {
        [Test]
        public void ResolveMovement_MovesAtMostOneLogicalStepTowardAttackPosition()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, ranged, BattleSide.Enemy, new HexCoord(2, 1))
                });

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(2, 1), simulation.Units[1].CurrentHex);
        }

        [Test]
        public void ResolveMovement_DoesNotMoveUnitAlreadyInAttackRange()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(2, 1))
                });

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(0, moved);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(2, 1), simulation.Units[1].CurrentHex);
        }

        [Test]
        public void ResolveMovement_ResolvesSameHexConflictDeterministically()
        {
            var board = new HexBoard(5, 6, 1f);
            board.SetWalkable(new HexCoord(0, 0), false);
            board.SetWalkable(new HexCoord(0, 2), false);
            board.SetWalkable(new HexCoord(2, 0), false);
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 1)),
                    new UnitSpawnData(2, melee, BattleSide.Player, new HexCoord(1, 0)),
                    new UnitSpawnData(3, ranged, BattleSide.Enemy, new HexCoord(3, 1))
                });

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[1].CurrentHex);
            Assert.AreNotEqual(simulation.Units[0].CurrentHex, simulation.Units[1].CurrentHex);
        }

        [Test]
        public void ResolveMovement_DoesNotEndStepOnOccupiedHex()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Player, new HexCoord(1, 0)),
                    new UnitSpawnData(3, ranged, BattleSide.Enemy, new HexCoord(2, 1))
                });

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.AreNotEqual(new HexCoord(1, 0), simulation.Units[0].CurrentHex);
            Assert.AreNotEqual(simulation.Units[0].CurrentHex, simulation.Units[1].CurrentHex);
        }

        [Test]
        public void ResolveMovement_BlockedUnitKeepsValidState()
        {
            var board = new HexBoard(5, 6, 1f);
            board.SetWalkable(new HexCoord(1, 0), false);
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Player, new HexCoord(0, 1)),
                    new UnitSpawnData(3, ranged, BattleSide.Enemy, new HexCoord(2, 1))
                });

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[1].CurrentHex);
            Assert.AreNotEqual(simulation.Units[0].CurrentHex, simulation.Units[1].CurrentHex);
            Assert.IsTrue(simulation.TryGetUnitAt(new HexCoord(0, 0), out UnitRuntimeState occupyingUnit));
            Assert.AreSame(simulation.Units[0], occupyingUnit);
        }

        private static UnitDefinition CreateUnit(string unitId, int attackRange)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.AttackRange = attackRange;
            return definition;
        }
    }
}

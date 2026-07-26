using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class MovementResolverTests
    {
        [Test]
        public void ResolveMovement_StartsOneStepAndKeepsSourceOccupiedForStepDuration()
        {
            BattleSimulation simulation = CreateDuel(1, 3);

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
            Assert.That(
                simulation.Units[0].MovementTimeRemaining,
                Is.EqualTo(simulation.Tuning.MovementStepDuration).Within(0.000001f));
            Assert.IsTrue(simulation.TryGetUnitAt(new HexCoord(0, 0), out UnitRuntimeState occupant));
            Assert.AreSame(simulation.Units[0], occupant);
        }

        [Test]
        public void ResolveMovement_DoesNotMoveUnitAlreadyInAttackRange()
        {
            BattleSimulation simulation = CreateDuel(1, 2, 1);

            Assert.AreEqual(0, MovementResolver.ResolveMovement(simulation));
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
        }

        [Test]
        public void ResolveMovement_ContestedDestinationUsesDeploymentOrderNotPathLength()
        {
            HexBoard board = CreateContestedBoard();
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(board, new[]
            {
                new UnitSpawnData(4, melee, BattleSide.Player, new HexCoord(0, 1)),
                new UnitSpawnData(2, melee, BattleSide.Player, new HexCoord(1, 0)),
                new UnitSpawnData(7, ranged, BattleSide.Enemy, new HexCoord(3, 1))
            });

            Assert.AreEqual(1, MovementResolver.ResolveMovement(simulation));
            Assert.AreEqual(new HexCoord(0, 1), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[1].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[1].MovementDestination);
        }

        [Test]
        public void ResolveMovement_LoserDoesNotChooseAlternativeStep()
        {
            BattleSimulation simulation = CreateDuel(1, 2);

            Assert.AreEqual(1, MovementResolver.ResolveMovement(simulation));
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
            Assert.AreEqual(new HexCoord(2, 0), simulation.Units[1].CurrentHex);
        }

        [Test]
        public void PlanMovementDestinations_IsPureAndMatchesCommittedWinners()
        {
            BattleSimulation simulation = CreateDuel(1, 3);
            var workspace = new MovementResolver.Workspace(25, 2);
            var destinations = new Dictionary<int, HexCoord>();

            Assert.AreEqual(1, MovementResolver.PlanMovementDestinations(simulation, workspace, destinations));
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, simulation.Units[0].TargetUnitId);
            Assert.AreEqual(new HexCoord(1, 0), destinations[1]);

            Assert.AreEqual(1, MovementResolver.ResolveMovement(simulation, workspace));
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
            Assert.IsTrue(simulation.Units[0].IsMoving);
        }

        private static BattleSimulation CreateDuel(int playerId, int enemyId, int enemyQ = 2)
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            return BattleSimulation.Create(new HexBoard(5, 5, 1f), new[]
            {
                new UnitSpawnData(playerId, melee, BattleSide.Player, new HexCoord(0, 0)),
                new UnitSpawnData(enemyId, melee, BattleSide.Enemy, new HexCoord(enemyQ, 0))
            });
        }

        private static HexBoard CreateContestedBoard()
        {
            var board = new HexBoard(5, 6, 1f);
            board.SetWalkable(new HexCoord(0, 0), false);
            board.SetWalkable(new HexCoord(0, 2), false);
            board.SetWalkable(new HexCoord(2, 0), false);
            return board;
        }

        private static UnitDefinition CreateUnit(string id, int attackRange)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(id, 1);
            definition.AttackRange = attackRange;
            return definition;
        }
    }
}

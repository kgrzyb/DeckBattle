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
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
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
        public void ResolveMovement_RandomizesReciprocalOneHexGapConflict()
        {
            BattleSimulation simulation = CreateReciprocalGapSimulation(2);

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.IsFalse(simulation.Units[1].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
            Assert.AreEqual(new HexCoord(2, 0), simulation.Units[1].CurrentHex);
        }

        [Test]
        public void ResolveMovement_ReciprocalOneHexGapLoserWaitsInsteadOfFindingAlternativeStep()
        {
            BattleSimulation simulation = CreateReciprocalGapSimulation(1);

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.IsFalse(simulation.Units[0].IsMoving);
            Assert.IsTrue(simulation.Units[1].IsMoving);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[1].MovementDestination);
        }

        [Test]
        public void ResolveMovement_ReciprocalOneHexGapLoserKeepsWaitingWhileWinnerMovesIntoRange()
        {
            BattleSimulation simulation = CreateReciprocalGapSimulation(2);
            var workspace = new MovementResolver.Workspace(25, 2);

            MovementResolver.ResolveMovement(simulation, 0f, workspace, null);
            int moved = MovementResolver.ResolveMovement(simulation, 0.1f, workspace, null);

            Assert.AreEqual(0, moved);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
            Assert.IsFalse(simulation.Units[1].IsMoving);
            Assert.AreEqual(new HexCoord(2, 0), simulation.Units[1].CurrentHex);
        }

        [Test]
        public void PlanMovementDestinations_ReciprocalOneHexGapDoesNotUseAlternativeStep()
        {
            BattleSimulation simulation = CreateReciprocalGapSimulation(2);
            var destinationsByUnitId = new System.Collections.Generic.Dictionary<int, HexCoord>(2);
            var workspace = new MovementResolver.Workspace(25, 2);

            int planned = MovementResolver.PlanMovementDestinations(simulation, workspace, destinationsByUnitId);

            Assert.AreEqual(1, planned);
            Assert.IsTrue(destinationsByUnitId.ContainsKey(1));
            Assert.IsFalse(destinationsByUnitId.ContainsKey(2));
            Assert.AreEqual(new HexCoord(1, 0), destinationsByUnitId[1]);
        }

        [Test]
        public void PlanMovementDestinations_ReciprocalOneHexGapDoesNotConsumeMovementRandom()
        {
            BattleSimulation simulation = CreateReciprocalGapSimulation(1);
            var destinationsByUnitId = new System.Collections.Generic.Dictionary<int, HexCoord>(2);
            var workspace = new MovementResolver.Workspace(25, 2);

            MovementResolver.PlanMovementDestinations(simulation, workspace, destinationsByUnitId);
            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.IsFalse(simulation.Units[0].IsMoving);
            Assert.IsTrue(simulation.Units[1].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[1].MovementDestination);
        }

        [Test]
        public void ResolveMovement_ChasesReachableCurrentTarget_WhenAnotherEnemyIsInRange()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(4, 0)),
                    new UnitSpawnData(3, melee, BattleSide.Enemy, new HexCoord(0, 1))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);

            MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(2, simulation.Units[0].TargetUnitId);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
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
            Assert.AreEqual(new HexCoord(0, 1), simulation.Units[0].CurrentHex);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[0].MovementDestination);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[1].CurrentHex);
            Assert.AreNotEqual(simulation.Units[0].MovementDestination, simulation.Units[1].CurrentHex);
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

            Assert.AreEqual(2, moved);
            Assert.AreNotEqual(new HexCoord(1, 0), simulation.Units[0].MovementDestination);
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
            Assert.IsTrue(simulation.Units[1].IsMoving);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[1].MovementDestination);
            Assert.AreNotEqual(simulation.Units[0].CurrentHex, simulation.Units[1].CurrentHex);
            Assert.IsTrue(simulation.TryGetUnitAt(new HexCoord(0, 0), out UnitRuntimeState occupyingUnit));
            Assert.AreSame(simulation.Units[0], occupyingUnit);
        }

        [Test]
        public void ResolveMovement_CommitsMoveAfterGlobalMovementStepDuration()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, ranged, BattleSide.Enemy, new HexCoord(2, 0))
                },
                new BattleRuntimeTuning(1f, 0, 0.5f));
            var workspace = new MovementResolver.Workspace(30, 2);

            MovementResolver.ResolveMovement(simulation, 0.1f, workspace, null);
            MovementResolver.ResolveMovement(simulation, 0.5f, workspace, null);

            Assert.IsFalse(simulation.Units[0].IsMoving);
            Assert.AreEqual(new HexCoord(1, 0), simulation.Units[0].CurrentHex);
        }

        [Test]
        public void ResolveMovement_MovingUnitBlocksCurrentAndDestinationHexes()
        {
            UnitDefinition melee = CreateUnit("melee", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(3, ranged, BattleSide.Enemy, new HexCoord(3, 0))
                },
                new BattleRuntimeTuning(1f, 0, 1f));
            simulation.StartUnitMovement(simulation.Units[0], new HexCoord(1, 0));

            int moved = MovementResolver.ResolveMovement(simulation, 0.1f, new MovementResolver.Workspace(30, 3), null);

            Assert.AreEqual(1, moved);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[1].CurrentHex);
            Assert.IsTrue(simulation.Units[1].IsMoving);
            Assert.AreNotEqual(new HexCoord(0, 0), simulation.Units[1].MovementDestination);
            Assert.AreNotEqual(new HexCoord(1, 0), simulation.Units[1].MovementDestination);
        }

        [Test]
        public void ResolveMovement_CloserUnitWinsContestedHexWithGlobalMovementSpeed()
        {
            var board = new HexBoard(5, 6, 1f);
            board.SetWalkable(new HexCoord(0, 0), false);
            board.SetWalkable(new HexCoord(0, 2), false);
            board.SetWalkable(new HexCoord(2, 0), false);
            UnitDefinition first = CreateUnit("first", 1);
            UnitDefinition second = CreateUnit("second", 1);
            UnitDefinition ranged = CreateUnit("ranged", 5);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, first, BattleSide.Player, new HexCoord(0, 1)),
                    new UnitSpawnData(2, second, BattleSide.Player, new HexCoord(1, 0)),
                    new UnitSpawnData(3, ranged, BattleSide.Enemy, new HexCoord(3, 1))
                });

            int moved = MovementResolver.ResolveMovement(simulation);

            Assert.AreEqual(1, moved);
            Assert.IsTrue(simulation.Units[0].IsMoving);
            Assert.IsFalse(simulation.Units[1].IsMoving);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[0].MovementDestination);
        }

        private static UnitDefinition CreateUnit(string unitId, int attackRange)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.AttackRange = attackRange;
            return definition;
        }

        private static BattleSimulation CreateReciprocalGapSimulation(int randomSeed)
        {
            UnitDefinition player = CreateUnit("player", 1);
            UnitDefinition enemy = CreateUnit("enemy", 1);
            return BattleSimulation.Create(
                new HexBoard(5, 5, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                },
                BattleRuntimeTuning.Default,
                randomSeed);
        }
    }
}

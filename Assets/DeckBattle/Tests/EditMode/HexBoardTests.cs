using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class HexBoardTests
    {
        [Test]
        public void Contains_ReturnsExpectedValues_ForFiveBySixBoard()
        {
            var board = new HexBoard(5, 6, 1f);

            Assert.IsTrue(board.Contains(new HexCoord(0, 0)));
            Assert.IsTrue(board.Contains(new HexCoord(4, 5)));
            Assert.IsFalse(board.Contains(new HexCoord(-1, 0)));
            Assert.IsFalse(board.Contains(new HexCoord(5, 0)));
            Assert.IsFalse(board.Contains(new HexCoord(0, 6)));

            Assert.IsTrue(board.IsValidHex(new HexCoord(0, 0)));
            Assert.IsFalse(board.IsValidHex(new HexCoord(0, 6)));
        }

        [Test]
        public void FillNeighbors_ReturnsValidNeighborsOnly()
        {
            var board = new HexBoard(5, 6, 1f);
            var neighbors = new List<HexCoord>(6);

            int count = board.FillNeighbors(new HexCoord(2, 2), neighbors);

            Assert.AreEqual(6, count);
            Assert.AreEqual(6, neighbors.Count);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Assert.IsTrue(board.Contains(neighbors[i]));
            }
        }

        [Test]
        public void FillNeighbors_ReturnsExpectedOffsetRowNeighbors_ForEvenInteriorHex()
        {
            var board = new HexBoard(5, 6, 1f);
            var neighbors = new List<HexCoord>(6);

            board.FillNeighbors(new HexCoord(2, 2), neighbors);

            CollectionAssert.AreEqual(
                new[]
                {
                    new HexCoord(3, 2),
                    new HexCoord(2, 1),
                    new HexCoord(1, 1),
                    new HexCoord(1, 2),
                    new HexCoord(1, 3),
                    new HexCoord(2, 3)
                },
                neighbors);
        }

        [Test]
        public void FillNeighbors_ReturnsExpectedOffsetRowNeighbors_ForOddInteriorHex()
        {
            var board = new HexBoard(5, 6, 1f);
            var neighbors = new List<HexCoord>(6);

            board.FillNeighbors(new HexCoord(2, 3), neighbors);

            CollectionAssert.AreEqual(
                new[]
                {
                    new HexCoord(3, 3),
                    new HexCoord(3, 2),
                    new HexCoord(2, 2),
                    new HexCoord(1, 3),
                    new HexCoord(2, 4),
                    new HexCoord(3, 4)
                },
                neighbors);
        }

        [Test]
        public void Distance_UsesOffsetRowHexDistance()
        {
            var board = new HexBoard(5, 6, 1f);

            Assert.AreEqual(0, board.Distance(new HexCoord(1, 1), new HexCoord(1, 1)));
            Assert.AreEqual(1, board.Distance(new HexCoord(1, 1), new HexCoord(2, 1)));
            Assert.AreEqual(3, board.Distance(new HexCoord(0, 0), new HexCoord(2, 2)));
            Assert.AreEqual(2, board.Distance(new HexCoord(3, 3), new HexCoord(2, 4)));
        }

        [Test]
        public void IsWalkable_ReturnsFalse_ForBlockedHex()
        {
            var board = new HexBoard(5, 6, 1f);
            HexCoord blocked = new HexCoord(2, 2);

            Assert.IsTrue(board.IsWalkable(blocked));

            board.SetWalkable(blocked, false);

            Assert.IsFalse(board.IsWalkable(blocked));
            Assert.IsFalse(board.IsWalkable(new HexCoord(5, 2)));

            board.SetWalkable(blocked, true);

            Assert.IsTrue(board.IsWalkable(blocked));
        }

        [Test]
        public void FillHexesInRange_ReturnsValidHexesWithinDistance()
        {
            var board = new HexBoard(5, 6, 1f);
            var hexes = new List<HexCoord>(8);

            int count = board.FillHexesInRange(new HexCoord(2, 2), 1, hexes);

            Assert.AreEqual(7, count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new HexCoord(1, 1),
                    new HexCoord(1, 2),
                    new HexCoord(2, 1),
                    new HexCoord(2, 2),
                    new HexCoord(2, 3),
                    new HexCoord(1, 3),
                    new HexCoord(3, 2)
                },
                hexes);
        }

        [Test]
        public void FillHexesInRange_ClipsToBoardBounds()
        {
            var board = new HexBoard(5, 6, 1f);
            var hexes = new List<HexCoord>(4);

            int count = board.FillHexesInRange(new HexCoord(0, 0), 1, hexes);

            Assert.AreEqual(3, count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new HexCoord(0, 0),
                    new HexCoord(0, 1),
                    new HexCoord(1, 0)
                },
                hexes);
        }

        [Test]
        public void TryFindPath_ReturnsShortestPath()
        {
            var board = new HexBoard(5, 6, 1f);
            var path = new List<HexCoord>(4);

            bool found = board.TryFindPath(new HexCoord(0, 0), new HexCoord(2, 0), path);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(
                new[]
                {
                    new HexCoord(0, 0),
                    new HexCoord(1, 0),
                    new HexCoord(2, 0)
                },
                path);
        }

        [Test]
        public void TryFindPath_AvoidsBlockedHexes()
        {
            var board = new HexBoard(5, 6, 1f);
            var path = new List<HexCoord>(8);
            board.SetWalkable(new HexCoord(1, 0), false);

            bool found = board.TryFindPath(new HexCoord(0, 0), new HexCoord(2, 0), path);

            Assert.IsTrue(found);
            Assert.IsFalse(path.Contains(new HexCoord(1, 0)));
            Assert.AreEqual(new HexCoord(0, 0), path[0]);
            Assert.AreEqual(new HexCoord(2, 0), path[path.Count - 1]);
        }

        [Test]
        public void TryFindPath_AvoidsAdditionalBlockedHexes()
        {
            var board = new HexBoard(5, 6, 1f);
            var path = new List<HexCoord>(8);
            var dynamicBlocked = new HashSet<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0)
            };
            var workspace = new HexBoard.PathfindingWorkspace(board.Width * board.Height);

            bool found = board.TryFindPath(new HexCoord(0, 0), new HexCoord(2, 0), path, workspace, dynamicBlocked);

            Assert.IsTrue(found);
            Assert.IsFalse(path.Contains(new HexCoord(1, 0)));
            Assert.AreEqual(new HexCoord(0, 0), path[0]);
            Assert.AreEqual(new HexCoord(2, 0), path[path.Count - 1]);
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenGoalIsNotWalkable()
        {
            var board = new HexBoard(5, 6, 1f);
            var path = new List<HexCoord>(4) { new HexCoord(0, 0) };
            board.SetWalkable(new HexCoord(2, 0), false);

            bool found = board.TryFindPath(new HexCoord(0, 0), new HexCoord(2, 0), path);

            Assert.IsFalse(found);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void TryFindShortestPathToAny_SelectsStableCoordinateAmongEquallyNearGoals()
        {
            var board = new HexBoard(5, 6, 1f);
            var goals = new List<HexCoord>
            {
                new HexCoord(2, 0),
                new HexCoord(0, 2)
            };
            var path = new List<HexCoord>(8);
            var workspace = new HexBoard.PathfindingWorkspace(board.Width * board.Height);

            bool found = board.TryFindShortestPathToAny(
                new HexCoord(0, 0),
                goals,
                path,
                workspace,
                out HexCoord selectedGoal,
                out HexCoord nextStep,
                out int pathSteps);

            Assert.IsTrue(found);
            Assert.AreEqual(new HexCoord(0, 2), selectedGoal);
            Assert.AreEqual(new HexCoord(0, 1), nextStep);
            Assert.AreEqual(2, pathSteps);
            Assert.AreEqual(selectedGoal, path[path.Count - 1]);
        }

        [Test]
        public void TryFindShortestPathToAny_AvoidsDynamicBlockedHexesAndReusesWorkspace()
        {
            var board = new HexBoard(5, 6, 1f);
            var goals = new List<HexCoord> { new HexCoord(2, 0) };
            var path = new List<HexCoord>(8);
            var blocked = new HashSet<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0)
            };
            var workspace = new HexBoard.PathfindingWorkspace(board.Width * board.Height);

            Assert.IsTrue(board.TryFindShortestPathToAny(
                new HexCoord(0, 0),
                goals,
                path,
                workspace,
                blocked,
                out _,
                out _,
                out int firstSteps));
            Assert.IsFalse(path.Contains(new HexCoord(1, 0)));

            blocked.Clear();
            Assert.IsTrue(board.TryFindShortestPathToAny(
                new HexCoord(0, 0),
                goals,
                path,
                workspace,
                blocked,
                out _,
                out _,
                out int secondSteps));
            Assert.Less(secondSteps, firstSteps);
        }

        [Test]
        public void DeploymentZones_AreSeparatedBySide()
        {
            var board = new HexBoard(5, 6, 1f);

            Assert.IsTrue(board.IsDeploymentCoord(BattleSide.Player, new HexCoord(2, 0)));
            Assert.IsTrue(board.IsDeploymentCoord(BattleSide.Player, new HexCoord(2, 2)));
            Assert.IsFalse(board.IsDeploymentCoord(BattleSide.Player, new HexCoord(2, 3)));

            Assert.IsTrue(board.IsDeploymentCoord(BattleSide.Enemy, new HexCoord(2, 5)));
            Assert.IsTrue(board.IsDeploymentCoord(BattleSide.Enemy, new HexCoord(2, 3)));
            Assert.IsFalse(board.IsDeploymentCoord(BattleSide.Enemy, new HexCoord(2, 2)));
        }

        [Test]
        public void ToLocalPosition_CentersBoardAroundOrigin()
        {
            var board = new HexBoard(5, 6, 1f);

            Vector3 first = board.ToLocalPosition(new HexCoord(0, 0));
            Vector3 last = board.ToLocalPosition(new HexCoord(4, 5));

            Assert.That(first.x + last.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(first.z + last.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ToLocalPosition_KeepsRowsVisuallySymmetric()
        {
            var board = new HexBoard(5, 6, 1f);

            Vector3 bottomLeft = board.ToLocalPosition(new HexCoord(0, 0));
            Vector3 bottomRight = board.ToLocalPosition(new HexCoord(4, 0));
            Vector3 topLeft = board.ToLocalPosition(new HexCoord(0, 5));
            Vector3 topRight = board.ToLocalPosition(new HexCoord(4, 5));

            Assert.That(bottomRight.x - bottomLeft.x, Is.EqualTo(topRight.x - topLeft.x).Within(0.001f));
            Assert.That(Mathf.Abs(bottomLeft.x), Is.EqualTo(Mathf.Abs(topRight.x)).Within(0.001f));
            Assert.That(Mathf.Abs(bottomRight.x), Is.EqualTo(Mathf.Abs(topLeft.x)).Within(0.001f));
        }

        [Test]
        public void ToLocalPosition_MatchesOffsetRowNeighborGeometry()
        {
            var board = new HexBoard(5, 6, 1f);
            HexCoord center = new HexCoord(2, 2);
            var neighbors = new List<HexCoord>(6);

            board.FillNeighbors(center, neighbors);

            float expectedDistance = Vector3.Distance(board.ToLocalPosition(center), board.ToLocalPosition(neighbors[0]));
            for (int i = 1; i < neighbors.Count; i++)
            {
                float distance = Vector3.Distance(board.ToLocalPosition(center), board.ToLocalPosition(neighbors[i]));
                Assert.That(distance, Is.EqualTo(expectedDistance).Within(0.001f));
            }

            Assert.That(
                Vector3.Distance(board.ToLocalPosition(new HexCoord(3, 3)), board.ToLocalPosition(new HexCoord(3, 4))),
                Is.EqualTo(expectedDistance).Within(0.001f));
            Assert.That(
                Vector3.Distance(board.ToLocalPosition(new HexCoord(3, 3)), board.ToLocalPosition(new HexCoord(2, 4))),
                Is.GreaterThan(expectedDistance));
        }
    }
}

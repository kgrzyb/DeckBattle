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
        public void Distance_UsesAxialHexDistance()
        {
            var board = new HexBoard(5, 6, 1f);

            Assert.AreEqual(0, board.Distance(new HexCoord(1, 1), new HexCoord(1, 1)));
            Assert.AreEqual(1, board.Distance(new HexCoord(1, 1), new HexCoord(2, 1)));
            Assert.AreEqual(4, board.Distance(new HexCoord(0, 0), new HexCoord(2, 2)));
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
    }
}

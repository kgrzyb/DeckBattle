using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class HexBoardLayoutTests
    {
        [Test]
        public void Matches_RequiresSameTopologyAndHexSize()
        {
            var layout = new HexBoardLayout(new HexBoard(5, 6, 1f));

            Assert.IsTrue(layout.Matches(new HexBoard(5, 6, 1f)));
            Assert.IsFalse(layout.Matches(new HexBoard(4, 6, 1f)));
            Assert.IsFalse(layout.Matches(new HexBoard(5, 6, 1.25f)));
        }

        [Test]
        public void GetLocalCenter_IsMidpointOfBoardExtents()
        {
            var layout = new HexBoardLayout(new HexBoard(5, 6, 1f));

            Vector3 expected = (layout.GetLocalPosition(new HexCoord(0, 0))
                + layout.GetLocalPosition(new HexCoord(4, 5))) * 0.5f;

            Assert.AreEqual(expected, layout.GetLocalCenter());
        }
    }
}

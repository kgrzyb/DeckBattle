using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleCameraPanStateTests
    {
        [Test]
        public void Pan_ClampsHorizontalAndDepthOffsets()
        {
            var state = CreateState();

            state.Pan(new Vector2(-2f, -2f), 10f);

            Assert.AreEqual(new Vector2(4f, 3f), state.PanOffset);

            state.Pan(new Vector2(2f, 2f), 10f);

            Assert.AreEqual(new Vector2(-4f, -3f), state.PanOffset);
        }

        [Test]
        public void Zoom_ClampsToMinimumAndMaximumDistance()
        {
            var state = CreateState();

            state.Zoom(2f, 20f);
            Assert.AreEqual(20f, state.Distance);

            state.Zoom(-3f, 20f);
            Assert.AreEqual(40f, state.Distance);
        }

        [Test]
        public void Pan_WithZeroDeltaDoesNotChangeOffset()
        {
            var state = CreateState();

            bool didPan = state.Pan(Vector2.zero, 10f);

            Assert.IsFalse(didPan);
            Assert.AreEqual(Vector2.zero, state.PanOffset);
        }

        [Test]
        public void Constructor_NormalizesInvertedOrInvalidLimits()
        {
            var state = new BattleCameraPanState(
                float.PositiveInfinity,
                new Vector2(4f, -4f),
                new Vector2(float.NaN, 2f),
                -1f,
                float.NaN);

            state.Pan(new Vector2(-1f, -1f), 10f);
            state.Zoom(-10f, 100f);

            Assert.GreaterOrEqual(state.PanOffset.x, -4f);
            Assert.LessOrEqual(state.PanOffset.x, 4f);
            Assert.GreaterOrEqual(state.PanOffset.y, 0f);
            Assert.LessOrEqual(state.PanOffset.y, 2f);
            Assert.GreaterOrEqual(state.Distance, 0.1f);
        }

        private static BattleCameraPanState CreateState()
        {
            return new BattleCameraPanState(
                30f,
                new Vector2(-4f, 4f),
                new Vector2(-3f, 3f),
                20f,
                40f);
        }
    }
}

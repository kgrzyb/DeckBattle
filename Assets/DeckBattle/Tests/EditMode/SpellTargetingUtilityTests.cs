using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class SpellTargetingUtilityTests
    {
        [Test]
        public void TryFindFriendlyUnitAtCoord_FindsAliveUnit()
        {
            BattleState state = CreateState();
            RuntimeUnit unit = AddPlayerUnit(state, new HexCoord(1, 2));

            RuntimeUnit found;
            bool result = SpellTargetingUtility.TryFindFriendlyUnitAtCoord(state.Player, new HexCoord(1, 2), out found);

            Assert.IsTrue(result);
            Assert.AreSame(unit, found);
        }

        [Test]
        public void TryFindFriendlyUnitAtCoord_IgnoresDeadUnits()
        {
            BattleState state = CreateState();
            RuntimeUnit unit = AddPlayerUnit(state, new HexCoord(1, 2));
            unit.CurrentHp = 0;

            RuntimeUnit found;
            bool result = SpellTargetingUtility.TryFindFriendlyUnitAtCoord(state.Player, new HexCoord(1, 2), out found);

            Assert.IsFalse(result);
            Assert.IsNull(found);
        }

        [Test]
        public void TryFindFriendlyUnitAtCoord_ReturnsFalseForEmptyHex()
        {
            BattleState state = CreateState();
            AddPlayerUnit(state, new HexCoord(1, 2));

            RuntimeUnit found;
            bool result = SpellTargetingUtility.TryFindFriendlyUnitAtCoord(state.Player, new HexCoord(2, 2), out found);

            Assert.IsFalse(result);
            Assert.IsNull(found);
        }

        private static BattleState CreateState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            var playerDeck = new CardDefinition[]
            {
                TestDefinitions.CreateUnit("guard", 1)
            };
            var enemyDeck = new CardDefinition[]
            {
                TestDefinitions.CreateUnit("enemy-guard", 1)
            };

            return BattleState.Create(config, playerDeck, enemyDeck, 42);
        }

        private static RuntimeUnit AddPlayerUnit(BattleState state, HexCoord coord)
        {
            var unit = new RuntimeUnit(state.AllocateRuntimeUnitId(), TestDefinitions.CreateUnit("player-unit", 1), BattleSide.Player, coord);
            state.Player.Units.Add(unit);
            return unit;
        }
    }
}

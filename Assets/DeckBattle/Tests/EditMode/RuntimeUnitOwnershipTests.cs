using System;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class RuntimeUnitOwnershipTests
    {
        [Test]
        public void Constructor_RequiresStablePositiveRuntimeIdAndDefinition()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RuntimeUnit(0, definition, BattleSide.Player, new HexCoord(0, 0)));
            Assert.Throws<ArgumentNullException>(
                () => new RuntimeUnit(1, null, BattleSide.Player, new HexCoord(0, 0)));
        }

        [Test]
        public void ResetForRound_PreservesFormationOwnershipAndClearsCombatResult()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.MaxHp = 8;
            var unit = new RuntimeUnit(3, definition, BattleSide.Player, new HexCoord(0, 0));
            unit.BattleCoord = new HexCoord(2, 2);
            unit.CurrentHp = 1;
            unit.IsDefeated = true;

            unit.ResetForRound();

            Assert.AreEqual(new HexCoord(0, 0), unit.FormationCoord);
            Assert.AreEqual(new HexCoord(0, 0), unit.BattleCoord);
            Assert.AreEqual(8, unit.CurrentHp);
            Assert.IsFalse(unit.IsDefeated);
            Assert.IsTrue(unit.IsAlive);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class FormationServiceTests
    {
        [Test]
        public void MoveUnit_ChangesFormationAndBattleCoord_OnLegalTile()
        {
            BattleState state = CreateStateWithTwoUnits();
            RuntimeUnit unit = state.Player.Units[0];

            FormationMoveResult result = FormationService.MoveUnit(state, state.Player, unit, new HexCoord(2, 1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(new HexCoord(2, 1), unit.FormationCoord);
            Assert.AreEqual(new HexCoord(2, 1), unit.BattleCoord);
        }

        [Test]
        public void MoveUnit_RejectsOccupiedTile()
        {
            BattleState state = CreateStateWithTwoUnits();
            RuntimeUnit unit = state.Player.Units[0];

            FormationMoveResult result = FormationService.MoveUnit(state, state.Player, unit, state.Player.Units[1].FormationCoord);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(FormationMoveFailReason.TileOccupied, result.FailReason);
        }

        [Test]
        public void MoveUnit_RejectsInvalidSideTile()
        {
            BattleState state = CreateStateWithTwoUnits();
            RuntimeUnit unit = state.Player.Units[0];

            FormationMoveResult result = FormationService.MoveUnit(state, state.Player, unit, new HexCoord(0, 5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(FormationMoveFailReason.InvalidTile, result.FailReason);
        }

        [Test]
        public void RestoreFormationAndResetRoundHealth_ResetsBattleStateForUnits()
        {
            BattleState state = CreateStateWithTwoUnits();
            RuntimeUnit unit = state.Player.Units[0];
            unit.BattleCoord = new HexCoord(4, 5);
            unit.CurrentHp = 1;
            unit.IsDefeated = true;

            FormationService.RestoreFormationAndResetRoundHealth(state.Player);

            Assert.AreEqual(unit.FormationCoord, unit.BattleCoord);
            Assert.AreEqual(unit.Definition.MaxHp, unit.CurrentHp);
            Assert.IsFalse(unit.IsDefeated);
        }

        private static BattleState CreateStateWithTwoUnits()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            var playerDeck = new List<UnitDefinition>
            {
                TestDefinitions.CreateUnit("guard", 1),
                TestDefinitions.CreateUnit("swordsman", 1),
                TestDefinitions.CreateUnit("archer", 1),
                TestDefinitions.CreateUnit("scout", 1)
            };
            var enemyDeck = new List<UnitDefinition>
            {
                TestDefinitions.CreateUnit("enemy-guard", 1),
                TestDefinitions.CreateUnit("enemy-swordsman", 1),
                TestDefinitions.CreateUnit("enemy-archer", 1),
                TestDefinitions.CreateUnit("enemy-scout", 1)
            };
            BattleState state = BattleState.Create(config, playerDeck, enemyDeck, 42);

            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));
            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(1, 0));
            return state;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class PreparationTurnServiceTests
    {
        [Test]
        public void CreateState_StartsPreparationWithOnlyTheStarterActive()
        {
            BattleState state = CreateState();

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.IsFalse(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
            Assert.IsTrue(PreparationTurnService.CanPlayerPrepare(state));
            Assert.IsFalse(PreparationTurnService.CanEnemyPrepare(state));
        }

        [Test]
        public void PlayUnit_DoesNotMarkPlayerReadyOrAdvanceActiveSide()
        {
            BattleState state = CreateState();

            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));

            Assert.IsFalse(state.Player.IsReady);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void PlayerCanPlayMultipleUnitsInSinglePreparationPhase()
        {
            BattleState state = CreateState();
            state.Player.Ap = 2;

            PlayUnitResult first = UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));
            PlayUnitResult second = UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(1, 0));

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.Success);
            Assert.AreEqual(2, state.Player.Units.Count);
            Assert.IsFalse(state.Player.IsReady);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void MarkPlayerReady_WhenEnemyIsNotReady_PassesPreparationToEnemy()
        {
            BattleState state = CreateState();

            bool changed = PreparationTurnService.MarkPlayerReady(state);

            Assert.IsTrue(changed);
            Assert.IsTrue(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
            Assert.AreEqual(BattleSide.Enemy, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.IsFalse(PreparationTurnService.CanPlayerPrepare(state));
            Assert.IsTrue(PreparationTurnService.CanEnemyPrepare(state));
        }

        [Test]
        public void MarkPlayerReady_BlocksFurtherUnitPlays()
        {
            BattleState state = CreateState();
            PreparationTurnService.MarkPlayerReady(state);

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));

            Assert.AreEqual(PlayUnitFailReason.PlayerReady, reason);
        }

        [Test]
        public void MarkEnemyReady_WhenEnemyIsInactive_DoesNotMutateState()
        {
            BattleState state = CreateState();

            bool changed = PreparationTurnService.MarkEnemyReady(state);

            Assert.IsFalse(changed);
            Assert.IsFalse(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void MarkEnemyReady_AfterPlayerReady_StartsCombat()
        {
            BattleState state = CreateState();

            PreparationTurnService.MarkPlayerReady(state);
            bool changed = PreparationTurnService.MarkEnemyReady(state);

            Assert.IsTrue(changed);
            Assert.IsTrue(state.Player.IsReady);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
        }

        [Test]
        public void CreateState_WithEnemyStarter_OnlyAllowsEnemyToPrepare()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 7);

            Assert.AreEqual(BattleSide.Enemy, state.InitialPreparationSide);
            Assert.AreEqual(BattleSide.Enemy, state.ActivePreparationSide);
            Assert.IsFalse(PreparationTurnService.CanPlayerPrepare(state));
            Assert.IsTrue(PreparationTurnService.CanEnemyPrepare(state));
        }

        private static BattleState CreateState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            return BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);
        }

        private static List<UnitDefinition> CreateDeck(string prefix)
        {
            return new List<UnitDefinition>
            {
                TestDefinitions.CreateUnit(prefix + "-guard", 1),
                TestDefinitions.CreateUnit(prefix + "-swordsman", 1),
                TestDefinitions.CreateUnit(prefix + "-archer", 2, UnitType.Range),
                TestDefinitions.CreateUnit(prefix + "-scout", 1)
            };
        }
    }
}

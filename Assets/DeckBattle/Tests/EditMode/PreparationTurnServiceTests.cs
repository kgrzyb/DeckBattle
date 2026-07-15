using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class PreparationTurnServiceTests
    {
        [Test]
        public void CreateState_StartsPreparationWithPlayerActive()
        {
            BattleState state = CreateState();

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.IsFalse(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
        }

        [Test]
        public void CompleteActiveSideAction_AfterPlayerPlay_AdvancesToEnemy()
        {
            BattleState state = CreateState();

            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));
            PreparationTurnService.CompleteActiveSideAction(state);

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(BattleSide.Enemy, state.ActivePreparationSide);
        }

        [Test]
        public void CompleteActiveSideAction_WhenPlayerCannotPlayMore_MarksPlayerReady()
        {
            BattleState state = CreateState();
            state.Player.Ap = 1;

            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));
            PreparationTurnService.CompleteActiveSideAction(state);

            Assert.IsTrue(state.Player.IsReady);
            Assert.AreEqual(BattleSide.Enemy, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void MarkActiveSideReadyAndAdvance_MarksPlayerReadyAndPassesTurn()
        {
            BattleState state = CreateState();

            PreparationTurnService.MarkActiveSideReadyAndAdvance(state);

            Assert.IsTrue(state.Player.IsReady);
            Assert.AreEqual(BattleSide.Enemy, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void MarkActiveSideReadyAndAdvance_WhenEnemyCannotAct_StartsCombat()
        {
            BattleState state = CreateState();
            state.Enemy.Ap = 0;

            PreparationTurnService.MarkActiveSideReadyAndAdvance(state);

            Assert.IsTrue(state.Player.IsReady);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
        }

        [Test]
        public void CompleteActiveSideAction_WhenOpponentReady_ReturnsTurnToPlayer()
        {
            BattleState state = CreateState();
            state.Enemy.IsReady = true;

            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));
            PreparationTurnService.CompleteActiveSideAction(state);

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
        }

        [Test]
        public void CreateState_WhenNeitherSideCanPlay_StartsCombat()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingAp = 0;

            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);

            Assert.IsTrue(state.Player.IsReady);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
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

using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class PreparationTurnServiceTests
    {
        [Test]
        public void CreateState_StartsPreparationWithBothSidesUnready()
        {
            BattleState state = CreateState();

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.IsFalse(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
            Assert.IsTrue(PreparationTurnService.CanPlayerPrepare(state));
            Assert.IsTrue(PreparationTurnService.CanEnemyPrepare(state));
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

            PlayUnitResult first = UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));
            PlayUnitResult second = UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(1, 0));

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.Success);
            Assert.AreEqual(2, state.Player.Units.Count);
            Assert.IsFalse(state.Player.IsReady);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void MarkPlayerReady_WhenEnemyIsNotReady_StaysInPreparation()
        {
            BattleState state = CreateState();

            PreparationTurnService.MarkPlayerReady(state);

            Assert.IsTrue(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.IsFalse(PreparationTurnService.CanPlayerPrepare(state));
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
        public void TryStartCombatIfReady_RequiresBothSidesReady()
        {
            BattleState state = CreateState();

            PreparationTurnService.MarkPlayerReady(state);
            bool startedBeforeEnemyReady = PreparationTurnService.TryStartCombatIfReady(state);
            PreparationTurnService.MarkEnemyReady(state);

            Assert.IsFalse(startedBeforeEnemyReady);
            Assert.IsTrue(state.Player.IsReady);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
        }

        [Test]
        public void ShouldStartPreparationCountdown_WhenOneSideReadyAndOnlyRepositionActionsRemain_ReturnsTrue()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingAp = 0;
            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);

            state.Enemy.IsReady = true;

            Assert.IsTrue(PreparationTurnService.HasOnlyRepositionActions(state));
            Assert.IsTrue(PreparationTurnService.ShouldStartPreparationCountdown(state));
        }

        [Test]
        public void CompletePreparationCountdown_MarksBothSidesReadyAndStartsCombat()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingAp = 0;
            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);
            PreparationTurnService.MarkPlayerReady(state);
            state.StartPreparationCountdown(10f);

            bool elapsed = state.TickPreparationCountdown(10f);
            state.CompletePreparationCountdown();

            Assert.IsTrue(elapsed);
            Assert.IsTrue(state.Player.IsReady);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.IsFalse(state.PreparationCountdownActive);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
        }

        [Test]
        public void CanPlayAnyUnit_IgnoresNonUnitCards()
        {
            BattleState state = CreateState();
            state.Player.Hand.Clear();
            CardDefinition definition = TestDefinitions.CreateSpell("firebolt", 0);
            var card = new CardRuntimeState(90, definition);
            card.Location = CardLocation.Hand;
            state.Player.Hand.Add(card);

            Assert.IsFalse(PreparationTurnService.CanPlayAnyUnit(state, state.Player));
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

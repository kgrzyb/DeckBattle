using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class DeckHandUnitPlayTests
    {
        [Test]
        public void DrawCards_MovesCardsFromDeckToHand()
        {
            BattleState state = CreateState();
            int beforeDeck = state.Player.Deck.Count;
            int beforeHand = state.Player.Hand.Count;

            int drawn = DeckService.DrawCards(state.Player, 1);

            Assert.AreEqual(1, drawn);
            Assert.AreEqual(beforeDeck - 1, state.Player.Deck.Count);
            Assert.AreEqual(beforeHand + 1, state.Player.Hand.Count);
            Assert.AreEqual(CardLocation.Hand, state.Player.Hand[state.Player.Hand.Count - 1].Location);
        }

        [Test]
        public void DrawCards_DoesNotDrawPastMaxHandSize()
        {
            BattleState state = CreateState();
            int beforeDeck = state.Player.Deck.Count;
            int beforeHand = state.Player.Hand.Count;

            int drawn = DeckService.DrawCards(state.Player, 1, beforeHand);

            Assert.AreEqual(0, drawn);
            Assert.AreEqual(beforeDeck, state.Player.Deck.Count);
            Assert.AreEqual(beforeHand, state.Player.Hand.Count);
        }

        [Test]
        public void CreateDeck_AcceptsBaseCardDefinitionsAndKeepsRuntimeIds()
        {
            int nextRuntimeCardId = 7;
            var definitions = new List<CardDefinition>
            {
                TestDefinitions.CreateUnit("guard", 1),
                TestDefinitions.CreateSpell("firebolt", 2)
            };
            var deck = new List<CardRuntimeState>();

            DeckService.CreateDeck(definitions, deck, ref nextRuntimeCardId);

            Assert.AreEqual(2, deck.Count);
            Assert.AreEqual(7, deck[0].RuntimeCardId);
            Assert.AreEqual(8, deck[1].RuntimeCardId);
            Assert.AreSame(definitions[0], deck[0].Definition);
            Assert.AreSame(definitions[1], deck[1].Definition);
            Assert.AreEqual(9, nextRuntimeCardId);
        }

        [Test]
        public void CanPayForCard_UsesBaseCardDefinitionCost()
        {
            BattleState state = CreateState();
            CardDefinition definition = TestDefinitions.CreateSpell("firebolt", 2);
            var card = new CardRuntimeState(50, definition);
            state.Player.Ap = 2;

            Assert.IsTrue(HandService.CanPayForCard(state.Player, card));

            state.Player.Ap = 1;

            Assert.IsFalse(HandService.CanPayForCard(state.Player, card));
        }

        [Test]
        public void PlayUnit_OnLegalTile_SpendsApAndCreatesUnit()
        {
            BattleState state = CreateState();
            CardRuntimeState card = state.Player.Hand[0];
            int startingAp = state.Player.Ap;

            PlayUnitResult result = UnitPlayService.PlayUnit(state, state.Player, card, new HexCoord(0, 0));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(PlayUnitFailReason.None, result.FailReason);
            Assert.AreEqual(startingAp - card.Definition.ApCost, state.Player.Ap);
            Assert.AreEqual(CardLocation.Played, card.Location);
            Assert.IsFalse(state.Player.Hand.Contains(card));
            Assert.AreEqual(1, state.Player.Units.Count);
            Assert.AreEqual(new HexCoord(0, 0), result.Unit.FormationCoord);
        }

        [Test]
        public void PlayUnit_RejectsNonUnitCard()
        {
            BattleState state = CreateState();
            CardRuntimeState card = new CardRuntimeState(51, TestDefinitions.CreateSpell("firebolt", 1));
            card.Location = CardLocation.Hand;
            state.Player.Hand.Add(card);

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, card, new HexCoord(0, 0));

            Assert.AreEqual(PlayUnitFailReason.InvalidCardType, reason);
        }

        [Test]
        public void PlayUnit_RejectsCardWithoutEnoughAp()
        {
            BattleState state = CreateState();
            CardRuntimeState card = state.Player.Hand[0];
            state.Player.Ap = card.Definition.ApCost - 1;

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, card, new HexCoord(0, 0));

            Assert.AreEqual(PlayUnitFailReason.NotEnoughAp, reason);
        }

        [Test]
        public void PlayUnit_RejectsWhenDeploymentSlotsAreFull()
        {
            BattleState state = CreateState();
            state.Player.DeploymentSlots = 0;

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));

            Assert.AreEqual(PlayUnitFailReason.NoDeploymentSlot, reason);
        }

        [Test]
        public void PlayUnit_RejectsEnemyDeploymentZone()
        {
            BattleState state = CreateState();

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, state.Player.Hand[0], new HexCoord(0, 5));

            Assert.AreEqual(PlayUnitFailReason.InvalidTile, reason);
        }

        [Test]
        public void PlayUnit_RejectsOccupiedTile()
        {
            BattleState state = CreateState();
            UnitPlayService.PlayUnit(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, state.Player.Hand[0], new HexCoord(0, 0));

            Assert.AreEqual(PlayUnitFailReason.TileOccupied, reason);
        }

        [Test]
        public void PlayUnit_RejectsAlreadyPlayedCard()
        {
            BattleState state = CreateState();
            CardRuntimeState card = state.Player.Hand[0];
            UnitPlayService.PlayUnit(state, state.Player, card, new HexCoord(0, 0));

            PlayUnitFailReason reason = UnitPlayService.ValidatePlay(state, state.Player, card, new HexCoord(1, 0));

            Assert.AreEqual(PlayUnitFailReason.UnitAlreadyPlayed, reason);
        }

        private static BattleState CreateState()
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

            return BattleState.Create(config, playerDeck, enemyDeck, 99);
        }

    }
}

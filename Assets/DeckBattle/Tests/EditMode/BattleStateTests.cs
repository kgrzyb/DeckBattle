using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleStateTests
    {
        [Test]
        public void Create_InitializesRoundOneStateAndStartingHands()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            List<UnitDefinition> playerDeck = CreateDeck(4);
            List<UnitDefinition> enemyDeck = CreateDeck(4);

            BattleState state = BattleState.Create(config, playerDeck, enemyDeck, 123);

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(1, state.RoundNumber);
            Assert.AreEqual(30, state.Player.Hp);
            Assert.AreEqual(30, state.Enemy.Hp);
            Assert.AreEqual(3, state.Player.Ap);
            Assert.AreEqual(3, state.Player.DeploymentSlots);
            Assert.AreEqual(0, state.Player.RoundDamageBonus);
            Assert.AreEqual(0, state.Enemy.RoundDamageBonus);
            Assert.AreEqual(3, state.Player.Hand.Count);
            Assert.AreEqual(1, state.Player.Deck.Count);
            Assert.AreEqual(3, state.Enemy.Hand.Count);
            Assert.AreEqual(5, state.Board.Width);
            Assert.AreEqual(6, state.Board.Height);
        }

        [Test]
        public void Create_DoesNotDrawStartingHandsPastMaxHandSize()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 5;
            config.MaxHandSize = 2;
            List<UnitDefinition> playerDeck = CreateDeck(5);
            List<UnitDefinition> enemyDeck = CreateDeck(5);

            BattleState state = BattleState.Create(config, playerDeck, enemyDeck, 123);

            Assert.AreEqual(2, state.Player.Hand.Count);
            Assert.AreEqual(3, state.Player.Deck.Count);
            Assert.AreEqual(2, state.Enemy.Hand.Count);
            Assert.AreEqual(3, state.Enemy.Deck.Count);
        }

        private static List<UnitDefinition> CreateDeck(int count)
        {
            var deck = new List<UnitDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                deck.Add(TestDefinitions.CreateUnit("unit-" + i, 1));
            }

            return deck;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class EnemyPreparationAITests
    {
        [Test]
        public void PrepareFormation_PlaysMultipleUnitsAndMarksEnemyReady()
        {
            BattleState state = CreateStateWithEnemyHand(
                TestDefinitions.CreateUnit("first", 1),
                TestDefinitions.CreateUnit("second", 1),
                TestDefinitions.CreateUnit("third", 1));

            EnemyPreparationAIResult result = EnemyPreparationAI.PrepareFormation(state);

            Assert.IsTrue(result.PlayedUnit);
            Assert.IsTrue(result.MarkedReady);
            Assert.AreEqual(3, result.PlayedUnitCount);
            Assert.AreEqual(3, state.Enemy.Units.Count);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
        }

        [Test]
        public void PrepareFormation_DoesNotSpendMoreThanAvailableAp()
        {
            BattleState state = CreateStateWithEnemyHand(
                TestDefinitions.CreateUnit("expensive", 4),
                TestDefinitions.CreateUnit("cheap", 2));
            state.Enemy.Ap = 2;

            EnemyPreparationAI.PrepareFormation(state);

            Assert.AreEqual(1, state.Enemy.Units.Count);
            Assert.AreEqual("cheap", state.Enemy.Units[0].Definition.UnitId);
            Assert.AreEqual(0, state.Enemy.Ap);
            Assert.IsTrue(state.Enemy.IsReady);
        }

        [Test]
        public void PrepareFormation_RespectsDeploymentSlots()
        {
            BattleState state = CreateState();
            state.Enemy.DeploymentSlots = 0;

            EnemyPreparationAIResult result = EnemyPreparationAI.PrepareFormation(state);

            Assert.IsFalse(result.PlayedUnit);
            Assert.IsTrue(result.MarkedReady);
            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(0, state.Enemy.Units.Count);
        }

        [Test]
        public void PrepareFormation_PlacesUnitsOnlyInEnemyDeploymentZone()
        {
            BattleState state = CreateState();

            EnemyPreparationAI.PrepareFormation(state);

            for (int i = 0; i < state.Enemy.Units.Count; i++)
            {
                Assert.IsTrue(state.Board.IsDeploymentCoord(BattleSide.Enemy, state.Enemy.Units[i].FormationCoord));
            }
        }

        [Test]
        public void PrepareFormation_PlacesMeleeCloserToFrontThanRange()
        {
            BattleState meleeState = CreateStateWithEnemyHand(TestDefinitions.CreateUnit("melee", 1, UnitType.Melee));
            BattleState rangeState = CreateStateWithEnemyHand(TestDefinitions.CreateUnit("range", 1, UnitType.Range));

            EnemyPreparationAI.PrepareFormation(meleeState);
            EnemyPreparationAI.PrepareFormation(rangeState);

            Assert.Less(meleeState.Enemy.Units[0].FormationCoord.R, rangeState.Enemy.Units[0].FormationCoord.R);
        }

        [Test]
        public void PrepareFormation_IsDeterministicForSameSeed()
        {
            BattleState first = CreateState();
            BattleState second = CreateState();

            EnemyPreparationAI.PrepareFormation(first);
            EnemyPreparationAI.PrepareFormation(second);

            Assert.AreEqual(first.Enemy.Units.Count, second.Enemy.Units.Count);
            Assert.AreEqual(first.Enemy.Units[0].Definition.UnitId, second.Enemy.Units[0].Definition.UnitId);
            Assert.AreEqual(first.Enemy.Units[0].FormationCoord, second.Enemy.Units[0].FormationCoord);
            Assert.AreEqual(first.Enemy.Ap, second.Enemy.Ap);
            Assert.IsTrue(first.Enemy.IsReady);
            Assert.IsTrue(second.Enemy.IsReady);
        }

        [Test]
        public void PrepareFormation_WhenPlayerAlreadyReady_StartsCombat()
        {
            BattleState state = CreateStateWithEnemyHand(
                TestDefinitions.CreateUnit("first", 1),
                TestDefinitions.CreateUnit("second", 1));
            state.Player.IsReady = true;

            EnemyPreparationAI.PrepareFormation(state);

            Assert.IsTrue(state.Enemy.IsReady);
            Assert.AreEqual(2, state.Enemy.Units.Count);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
        }

        private static BattleState CreateState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            return BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);
        }

        private static BattleState CreateStateWithEnemyHand(params UnitDefinition[] enemyHandDefinitions)
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);
            state.Enemy.Hand.Clear();
            state.Enemy.Deck.Clear();
            for (int i = 0; i < enemyHandDefinitions.Length; i++)
            {
                var card = new CardRuntimeState(100 + i, enemyHandDefinitions[i]);
                card.Location = CardLocation.Hand;
                state.Enemy.Hand.Add(card);
            }

            return state;
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

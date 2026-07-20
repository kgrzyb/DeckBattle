using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class RoundFlowTests
    {
        [Test]
        public void Resolve_CountsOnlyLivingSurvivorPowerAndAppliesHpDamage()
        {
            BattleState state = CreateState(0, 0);
            state.Player.Hp = 10;
            state.Enemy.Hp = 10;
            state.Phase = BattlePhase.RoundResolution;

            RuntimeUnit livingPlayer = CreateRuntimeUnit(1, BattleSide.Player, new HexCoord(0, 0), 4);
            RuntimeUnit defeatedPlayer = CreateRuntimeUnit(2, BattleSide.Player, new HexCoord(1, 0), 7);
            RuntimeUnit livingEnemy = CreateRuntimeUnit(3, BattleSide.Enemy, new HexCoord(0, 5), 3);
            defeatedPlayer.IsDefeated = true;
            state.Player.Units.Add(livingPlayer);
            state.Player.Units.Add(defeatedPlayer);
            state.Enemy.Units.Add(livingEnemy);

            RoundResolutionResult result = RoundDamageResolver.Resolve(state);

            Assert.AreEqual(4, result.PlayerDamageDealt);
            Assert.AreEqual(3, result.EnemyDamageDealt);
            Assert.AreEqual(7, state.Player.Hp);
            Assert.AreEqual(6, state.Enemy.Hp);
            Assert.IsFalse(result.MatchEnded);
            Assert.AreEqual(BattlePhase.RoundResolution, state.Phase);
        }

        [Test]
        public void Resolve_AddsRoundDamageBonusToSurvivorPowerForBothSides()
        {
            BattleState state = CreateState(0, 0);
            state.Player.Hp = 10;
            state.Enemy.Hp = 10;
            state.Player.RoundDamageBonus = 2;
            state.Enemy.RoundDamageBonus = 1;
            state.Phase = BattlePhase.RoundResolution;

            state.Player.Units.Add(CreateRuntimeUnit(1, BattleSide.Player, new HexCoord(0, 0), 4));
            state.Enemy.Units.Add(CreateRuntimeUnit(2, BattleSide.Enemy, new HexCoord(0, 5), 3));

            RoundResolutionResult result = RoundDamageResolver.Resolve(state);

            Assert.AreEqual(6, result.PlayerDamageDealt);
            Assert.AreEqual(4, result.EnemyDamageDealt);
            Assert.AreEqual(6, state.Player.Hp);
            Assert.AreEqual(4, state.Enemy.Hp);
            Assert.IsFalse(result.MatchEnded);
        }

        [Test]
        public void Resolve_WhenHpReachesZero_EndsMatchWithWinner()
        {
            BattleState state = CreateState(0, 0);
            state.Player.Hp = 2;
            state.Enemy.Hp = 10;
            state.Phase = BattlePhase.RoundResolution;
            state.Enemy.Units.Add(CreateRuntimeUnit(1, BattleSide.Enemy, new HexCoord(0, 5), 5));

            RoundResolutionResult result = RoundDamageResolver.Resolve(state);

            Assert.IsTrue(result.MatchEnded);
            Assert.IsTrue(result.HasWinner);
            Assert.AreEqual(BattleSide.Enemy, result.Winner);
            Assert.AreEqual(0, state.Player.Hp);
            Assert.AreEqual(BattlePhase.MatchEnd, state.Phase);
        }

        [Test]
        public void ResolveRoundAndStartNext_WhenMatchContinues_ResetsFormationResourcesAndDrawsCards()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 0;
            config.StartingAp = 3;
            config.MaxAp = 4;
            config.StartingDeploymentSlots = 2;
            config.MaxDeploymentSlots = 4;
            config.DeploymentSlotIncreaseEveryRounds = 1;
            config.DrawPerRound = 2;

            BattleState state = BattleState.Create(config, CreateDeck("player", 3), CreateDeck("enemy", 3), 7);
            RuntimeUnit playerUnit = CreateRuntimeUnit(1, BattleSide.Player, new HexCoord(1, 0), 0);
            RuntimeUnit enemyUnit = CreateRuntimeUnit(2, BattleSide.Enemy, new HexCoord(1, 5), 0);
            playerUnit.CurrentHp = 1;
            playerUnit.BattleCoord = new HexCoord(2, 2);
            playerUnit.IsDefeated = true;
            enemyUnit.CurrentHp = 1;
            enemyUnit.BattleCoord = new HexCoord(2, 3);
            enemyUnit.IsDefeated = true;
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            state.Player.Ap = 0;
            state.Enemy.Ap = 0;
            state.Player.IsReady = true;
            state.Enemy.IsReady = true;
            state.Phase = BattlePhase.RoundResolution;

            RoundResolutionResult result = RoundFlowService.ResolveRoundAndStartNext(state);

            Assert.IsFalse(result.MatchEnded);
            Assert.AreEqual(2, state.RoundNumber);
            Assert.AreEqual(BattlePhase.RoundStart, state.Phase);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
            Assert.AreEqual(4, state.Player.Ap);
            Assert.AreEqual(4, state.Enemy.Ap);
            Assert.AreEqual(3, state.Player.DeploymentSlots);
            Assert.AreEqual(3, state.Enemy.DeploymentSlots);
            Assert.AreEqual(2, state.Player.Hand.Count);
            Assert.AreEqual(2, state.Enemy.Hand.Count);
            Assert.IsFalse(state.Player.IsReady);
            Assert.IsFalse(state.Enemy.IsReady);
            Assert.AreEqual(playerUnit.Definition.MaxHp, playerUnit.CurrentHp);
            Assert.AreEqual(playerUnit.FormationCoord, playerUnit.BattleCoord);
            Assert.IsFalse(playerUnit.IsDefeated);
            Assert.AreEqual(enemyUnit.Definition.MaxHp, enemyUnit.CurrentHp);
            Assert.AreEqual(enemyUnit.FormationCoord, enemyUnit.BattleCoord);
            Assert.IsFalse(enemyUnit.IsDefeated);

            state.BeginPreparationAfterRoundStart();

            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(BattleSide.Player, state.ActivePreparationSide);
        }

        [Test]
        public void ResolveRoundAndStartNext_UsesIndependentProgressionCadenceAndCaps()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 0;
            config.DrawPerRound = 0;
            config.StartingAp = 2;
            config.ApIncreasePerStep = 2;
            config.ApIncreaseEveryRounds = 2;
            config.MaxAp = 4;
            config.StartingDeploymentSlots = 1;
            config.DeploymentSlotIncreasePerStep = 1;
            config.DeploymentSlotIncreaseEveryRounds = 1;
            config.MaxDeploymentSlots = 3;
            config.StartingRoundDamageBonus = 1;
            config.RoundDamageBonusIncreasePerStep = 3;
            config.RoundDamageBonusIncreaseEveryRounds = 3;
            config.MaxRoundDamageBonus = 4;

            BattleState state = BattleState.Create(config, CreateDeck("player", 0), CreateDeck("enemy", 0), 7);

            Assert.AreEqual(2, state.Player.Ap);
            Assert.AreEqual(1, state.Player.DeploymentSlots);
            Assert.AreEqual(1, state.Player.RoundDamageBonus);

            AdvanceRound(state);

            Assert.AreEqual(2, state.RoundNumber);
            Assert.AreEqual(BattlePhase.RoundStart, state.Phase);
            state.BeginPreparationAfterRoundStart();
            Assert.AreEqual(2, state.Player.Ap);
            Assert.AreEqual(2, state.Player.DeploymentSlots);
            Assert.AreEqual(1, state.Player.RoundDamageBonus);

            AdvanceRound(state);

            Assert.AreEqual(3, state.RoundNumber);
            Assert.AreEqual(BattlePhase.RoundStart, state.Phase);
            state.BeginPreparationAfterRoundStart();
            Assert.AreEqual(4, state.Player.Ap);
            Assert.AreEqual(3, state.Player.DeploymentSlots);
            Assert.AreEqual(1, state.Player.RoundDamageBonus);

            AdvanceRound(state);

            Assert.AreEqual(4, state.RoundNumber);
            Assert.AreEqual(BattlePhase.RoundStart, state.Phase);
            Assert.AreEqual(4, state.Player.Ap);
            Assert.AreEqual(3, state.Player.DeploymentSlots);
            Assert.AreEqual(4, state.Player.RoundDamageBonus);
            Assert.AreEqual(state.Player.Ap, state.Enemy.Ap);
            Assert.AreEqual(state.Player.DeploymentSlots, state.Enemy.DeploymentSlots);
            Assert.AreEqual(state.Player.RoundDamageBonus, state.Enemy.RoundDamageBonus);
        }

        [Test]
        public void ResolveRoundAndStartNext_DoesNotDrawPastMaxHandSize()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 2;
            config.MaxHandSize = 3;
            config.DrawPerRound = 5;

            BattleState state = BattleState.Create(config, CreateDeck("player", 6), CreateDeck("enemy", 6), 7);
            state.Phase = BattlePhase.RoundResolution;

            RoundFlowService.ResolveRoundAndStartNext(state);

            Assert.AreEqual(3, state.Player.Hand.Count);
            Assert.AreEqual(3, state.Enemy.Hand.Count);
            Assert.AreEqual(3, state.Player.Deck.Count);
            Assert.AreEqual(3, state.Enemy.Deck.Count);
        }

        [Test]
        public void ResolveRoundAndStartNext_WhenMatchEnds_ResetsUnitsToFormationWithoutStartingNextRound()
        {
            BattleState state = CreateState(0, 0);
            RuntimeUnit playerUnit = CreateRuntimeUnit(1, BattleSide.Player, new HexCoord(1, 0), 0);
            RuntimeUnit enemyUnit = CreateRuntimeUnit(2, BattleSide.Enemy, new HexCoord(1, 5), 5);
            playerUnit.CurrentHp = 0;
            playerUnit.BattleCoord = new HexCoord(2, 2);
            playerUnit.IsDefeated = true;
            enemyUnit.CurrentHp = 1;
            enemyUnit.BattleCoord = new HexCoord(2, 3);
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            state.Player.Hp = 2;
            state.Enemy.Hp = 10;
            state.Phase = BattlePhase.RoundResolution;

            RoundResolutionResult result = RoundFlowService.ResolveRoundAndStartNext(state);

            Assert.IsTrue(result.MatchEnded);
            Assert.AreEqual(BattlePhase.MatchEnd, state.Phase);
            Assert.AreEqual(1, state.RoundNumber);
            Assert.AreEqual(0, state.Player.Hp);
            Assert.AreEqual(playerUnit.Definition.MaxHp, playerUnit.CurrentHp);
            Assert.AreEqual(playerUnit.FormationCoord, playerUnit.BattleCoord);
            Assert.IsFalse(playerUnit.IsDefeated);
            Assert.AreEqual(enemyUnit.Definition.MaxHp, enemyUnit.CurrentHp);
            Assert.AreEqual(enemyUnit.FormationCoord, enemyUnit.BattleCoord);
            Assert.IsFalse(enemyUnit.IsDefeated);
        }

        private static BattleState CreateState(int playerDeckCount, int enemyDeckCount)
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 0;
            return BattleState.Create(config, CreateDeck("player", playerDeckCount), CreateDeck("enemy", enemyDeckCount), 42);
        }

        private static void AdvanceRound(BattleState state)
        {
            state.Phase = BattlePhase.RoundResolution;
            RoundFlowService.ResolveRoundAndStartNext(state);
        }

        private static RuntimeUnit CreateRuntimeUnit(int runtimeId, BattleSide side, HexCoord coord, int power)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(side + "-" + runtimeId, 1);
            definition.Power = power;
            definition.MaxHp = 5;
            return new RuntimeUnit(runtimeId, definition, side, coord);
        }

        private static List<UnitDefinition> CreateDeck(string prefix, int count)
        {
            var deck = new List<UnitDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                deck.Add(TestDefinitions.CreateUnit(prefix + "-" + i, 1));
            }

            return deck;
        }
    }
}

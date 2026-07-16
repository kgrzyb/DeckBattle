using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleSimulationCombatServiceTests
    {
        [Test]
        public void RunToResolution_WhenBattleEnded_SetsRoundResolutionAndSyncsRuntimeUnits()
        {
            BattleState state = CreateCombatState();
            UnitDefinition playerDefinition = CreateUnit("player-ranged", 5, 4, 3, 4);
            UnitDefinition enemyDefinition = CreateUnit("enemy", 3, 1, 1, 7);
            var playerUnit = new RuntimeUnit(1, playerDefinition, BattleSide.Player, new HexCoord(0, 0));
            var enemyUnit = new RuntimeUnit(2, enemyDefinition, BattleSide.Enemy, new HexCoord(2, 0));
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            var events = new BattleEventQueue();

            CombatSimulationResult result = BattleSimulationCombatService.RunToResolution(
                state,
                1f,
                5,
                BattleRuntimeTuning.Default,
                events);

            Assert.IsTrue(result.CombatEnded);
            Assert.AreEqual(CombatEndReason.OneSideDefeated, result.EndReason);
            Assert.IsTrue(result.HasWinner);
            Assert.AreEqual(BattleSide.Player, result.Winner);
            Assert.AreEqual(BattlePhase.RoundResolution, state.Phase);
            Assert.IsTrue(enemyUnit.IsDefeated);
            Assert.AreEqual(0, enemyUnit.CurrentHp);
            AssertEventTypeExists(events, BattleEventType.BattleEnded);

            RoundResolutionResult round = RoundFlowService.ResolveRoundAndStartNext(state);

            Assert.IsFalse(round.MatchEnded);
            Assert.AreEqual(4, round.PlayerDamageDealt);
            Assert.AreEqual(0, round.EnemyDamageDealt);
            Assert.AreEqual(BattlePhase.Preparation, state.Phase);
            Assert.AreEqual(new HexCoord(0, 0), playerUnit.BattleCoord);
            Assert.IsFalse(playerUnit.IsDefeated);
        }

        [Test]
        public void RunToResolution_WhenMaxTicksReached_SetsRoundResolutionAndKeepsIntermediateState()
        {
            BattleState state = CreateCombatState();
            UnitDefinition playerDefinition = CreateUnit("player", 10, 1, 1, 0);
            UnitDefinition enemyDefinition = CreateUnit("enemy", 10, 1, 1, 0);
            var playerUnit = new RuntimeUnit(1, playerDefinition, BattleSide.Player, new HexCoord(0, 0));
            var enemyUnit = new RuntimeUnit(2, enemyDefinition, BattleSide.Enemy, new HexCoord(4, 5));
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            var events = new BattleEventQueue();

            CombatSimulationResult result = BattleSimulationCombatService.RunToResolution(
                state,
                1f,
                1,
                BattleRuntimeTuning.Default,
                events);

            Assert.IsTrue(result.CombatEnded);
            Assert.AreEqual(CombatEndReason.MaxTicksReached, result.EndReason);
            Assert.AreEqual(BattlePhase.RoundResolution, state.Phase);
            Assert.IsTrue(playerUnit.IsAlive);
            Assert.IsTrue(enemyUnit.IsAlive);
        }

        private static BattleState CreateCombatState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 0;
            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 123);
            state.Phase = BattlePhase.Combat;
            return state;
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, int power)
        {
            UnitDefinition unit = TestDefinitions.CreateUnit(unitId, 1);
            unit.MaxHp = hp;
            unit.Attack = attack;
            unit.AttackRange = attackRange;
            unit.Power = power;
            unit.AttackCooldown = 1f;
            return unit;
        }

        private static List<UnitDefinition> CreateDeck(string prefix)
        {
            return new List<UnitDefinition>
            {
                TestDefinitions.CreateUnit(prefix + "-unit", 1)
            };
        }

        private static void AssertEventTypeExists(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return;
                }
            }

            Assert.Fail("Expected event type was not emitted: " + type);
        }
    }
}

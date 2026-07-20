using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleSimulationResultApplierTests
    {
        [Test]
        public void Apply_CopiesHpBattleCoordAndDefeatedStateToRuntimeUnits()
        {
            BattleState state = CreateState();
            UnitDefinition playerDefinition = TestDefinitions.CreateUnit("player", 1);
            UnitDefinition enemyDefinition = TestDefinitions.CreateUnit("enemy", 1);
            var playerUnit = new RuntimeUnit(10, playerDefinition, BattleSide.Player, new HexCoord(0, 0));
            var enemyUnit = new RuntimeUnit(20, enemyDefinition, BattleSide.Enemy, new HexCoord(4, 5));
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            BattleSimulation simulation = BattleSimulationFactory.Create(state);

            UnitRuntimeState simulatedPlayer;
            UnitRuntimeState simulatedEnemy;
            Assert.IsTrue(simulation.TryGetUnitById(10, out simulatedPlayer));
            Assert.IsTrue(simulation.TryGetUnitById(20, out simulatedEnemy));
            simulatedPlayer.CurrentHp = 2;
            simulation.MoveUnit(simulatedPlayer, new HexCoord(1, 1));
            simulation.DefeatUnit(simulatedEnemy);

            BattleSimulationResultApplier.Apply(state, simulation);

            Assert.AreEqual(2, playerUnit.CurrentHp);
            Assert.AreEqual(new HexCoord(1, 1), playerUnit.BattleCoord);
            Assert.IsFalse(playerUnit.IsDefeated);
            Assert.AreEqual(new HexCoord(0, 0), playerUnit.FormationCoord);
            Assert.AreEqual(0, enemyUnit.CurrentHp);
            Assert.AreEqual(new HexCoord(4, 5), enemyUnit.BattleCoord);
            Assert.IsTrue(enemyUnit.IsDefeated);
            Assert.AreEqual(new HexCoord(4, 5), enemyUnit.FormationCoord);
        }

        [Test]
        public void Apply_AllowsRoundDamageResolverToCountSimulationSurvivors()
        {
            BattleState state = CreateState();
            state.Player.Hp = 10;
            state.Enemy.Hp = 10;
            state.Phase = BattlePhase.RoundResolution;
            UnitDefinition playerDefinition = TestDefinitions.CreateUnit("player", 1);
            UnitDefinition enemyDefinition = TestDefinitions.CreateUnit("enemy", 1);
            playerDefinition.Power = 4;
            enemyDefinition.Power = 7;
            var playerUnit = new RuntimeUnit(1, playerDefinition, BattleSide.Player, new HexCoord(0, 0));
            var enemyUnit = new RuntimeUnit(2, enemyDefinition, BattleSide.Enemy, new HexCoord(2, 2));
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            BattleSimulation simulation = BattleSimulationFactory.Create(state);

            UnitRuntimeState simulatedEnemy;
            Assert.IsTrue(simulation.TryGetUnitById(2, out simulatedEnemy));
            simulation.DefeatUnit(simulatedEnemy);
            BattleSimulationResultApplier.Apply(state, simulation);

            RoundResolutionResult result = RoundDamageResolver.Resolve(state);

            Assert.AreEqual(4, result.PlayerDamageDealt);
            Assert.AreEqual(0, result.EnemyDamageDealt);
            Assert.AreEqual(10, state.Player.Hp);
            Assert.AreEqual(6, state.Enemy.Hp);
        }

        [Test]
        public void Apply_BeforeRoundFlow_AllowsNextRoundToRestoreFormation()
        {
            BattleState state = CreateState();
            state.Player.Hp = 20;
            state.Enemy.Hp = 20;
            state.Phase = BattlePhase.RoundResolution;
            UnitDefinition playerDefinition = TestDefinitions.CreateUnit("player", 1);
            UnitDefinition enemyDefinition = TestDefinitions.CreateUnit("enemy", 1);
            playerDefinition.Power = 0;
            enemyDefinition.Power = 0;
            var playerUnit = new RuntimeUnit(1, playerDefinition, BattleSide.Player, new HexCoord(0, 0));
            var enemyUnit = new RuntimeUnit(2, enemyDefinition, BattleSide.Enemy, new HexCoord(4, 5));
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            BattleSimulation simulation = BattleSimulationFactory.Create(state);

            UnitRuntimeState simulatedPlayer;
            Assert.IsTrue(simulation.TryGetUnitById(1, out simulatedPlayer));
            simulatedPlayer.CurrentHp = 2;
            simulation.MoveUnit(simulatedPlayer, new HexCoord(1, 1));
            BattleSimulationResultApplier.Apply(state, simulation);

            Assert.AreEqual(new HexCoord(1, 1), playerUnit.BattleCoord);
            Assert.AreEqual(new HexCoord(0, 0), playerUnit.FormationCoord);

            RoundResolutionResult result = RoundFlowService.ResolveRoundAndStartNext(state);

            Assert.IsFalse(result.MatchEnded);
            Assert.AreEqual(BattlePhase.RoundStart, state.Phase);
            Assert.AreEqual(playerDefinition.MaxHp, playerUnit.CurrentHp);
            Assert.AreEqual(new HexCoord(0, 0), playerUnit.BattleCoord);
            Assert.AreEqual(new HexCoord(0, 0), playerUnit.FormationCoord);
            Assert.IsFalse(playerUnit.IsDefeated);
        }

        private static BattleState CreateState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 0;
            return BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 123);
        }

        private static List<UnitDefinition> CreateDeck(string prefix)
        {
            return new List<UnitDefinition>
            {
                TestDefinitions.CreateUnit(prefix + "-unit", 1)
            };
        }
    }
}

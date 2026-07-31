using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleSimulationFactoryTests
    {
        [Test]
        public void Create_MapsLivingRuntimeUnitsToSimulationSpawnData()
        {
            BattleState state = CreateState();
            UnitDefinition playerDefinition = TestDefinitions.CreateUnit("player", 1);
            UnitDefinition enemyDefinition = TestDefinitions.CreateUnit("enemy", 1);
            var playerUnit = new RuntimeUnit(42, playerDefinition, BattleSide.Player, new HexCoord(0, 0));
            var enemyUnit = new RuntimeUnit(77, enemyDefinition, BattleSide.Enemy, new HexCoord(4, 5));
            playerUnit.BattleCoord = new HexCoord(1, 1);
            enemyUnit.BattleCoord = new HexCoord(3, 4);

            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);

            BattleSimulation simulation = BattleSimulationFactory.Create(state, new BattleRuntimeTuning(1f, 1));

            Assert.AreSame(state.Board, simulation.Board);
            Assert.AreEqual(2, simulation.Units.Count);
            Assert.AreEqual(42, simulation.Units[0].UnitId);
            Assert.AreEqual(UnitCombatSpec.FromDefinition(playerDefinition).DefinitionId, simulation.Units[0].CombatSpec.DefinitionId);
            Assert.AreEqual(BattleSide.Player, simulation.Units[0].Side);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[0].CurrentHex);
            Assert.AreEqual(77, simulation.Units[1].UnitId);
            Assert.AreEqual(UnitCombatSpec.FromDefinition(enemyDefinition).DefinitionId, simulation.Units[1].CombatSpec.DefinitionId);
            Assert.AreEqual(BattleSide.Enemy, simulation.Units[1].Side);
            Assert.AreEqual(new HexCoord(3, 4), simulation.Units[1].CurrentHex);
            Assert.AreEqual(2, simulation.Tuning.GetAttackRange(simulation.Units[0].CombatSpec));
        }

        [Test]
        public void Create_SkipsNullAndDefeatedRuntimeUnits()
        {
            BattleState state = CreateState();
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            var aliveUnit = new RuntimeUnit(1, definition, BattleSide.Player, new HexCoord(0, 0));
            var defeatedUnit = new RuntimeUnit(2, definition, BattleSide.Enemy, new HexCoord(1, 1));
            defeatedUnit.IsDefeated = true;

            state.Player.Units.Add(aliveUnit);
            state.Player.Units.Add(null);
            state.Enemy.Units.Add(defeatedUnit);

            BattleSimulation simulation = BattleSimulationFactory.Create(state);

            Assert.AreEqual(1, simulation.Units.Count);
            Assert.AreEqual(1, simulation.Units[0].UnitId);
            Assert.AreEqual(BattleSide.Player, simulation.Units[0].Side);
        }

        [Test]
        public void Create_UsesRuntimeTuningFromBattleConfig()
        {
            BattleState state = CreateState();
            state.Config.RuntimeTuningConfig.AttackCooldownMultiplier = 1.5f;
            state.Config.RuntimeTuningConfig.AttackRangeBonus = 2;
            state.Config.RuntimeTuningConfig.MovementStepDuration = 0.6f;
            state.Config.RuntimeTuningConfig.MaxDamageMultiplier = 5f;
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            state.Player.Units.Add(new RuntimeUnit(1, definition, BattleSide.Player, new HexCoord(0, 0)));

            BattleSimulation simulation = BattleSimulationFactory.Create(state);

            Assert.AreEqual(1.5f, simulation.Tuning.AttackCooldownMultiplier);
            Assert.AreEqual(2, simulation.Tuning.AttackRangeBonus);
            Assert.AreEqual(0.6f, simulation.Tuning.MovementStepDuration);
            Assert.AreEqual(5f, simulation.Tuning.MaxDamageMultiplier);
        }

        [Test]
        public void Create_RequiresRuntimeTuningConfig()
        {
            BattleState state = CreateState();
            state.Config.RuntimeTuningConfig = null;

            Assert.Throws<System.InvalidOperationException>(() => BattleSimulationFactory.Create(state));
        }

        private static BattleState CreateState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
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

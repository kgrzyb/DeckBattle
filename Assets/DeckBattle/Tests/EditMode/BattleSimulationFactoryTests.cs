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

            BattleSimulation simulation = BattleSimulationFactory.Create(state, new BattleRuntimeTuning(1f, 1, 2));

            Assert.AreSame(state.Board, simulation.Board);
            Assert.AreEqual(2, simulation.Units.Count);
            Assert.AreEqual(42, simulation.Units[0].UnitId);
            Assert.AreSame(playerDefinition, simulation.Units[0].Definition);
            Assert.AreEqual(BattleSide.Player, simulation.Units[0].Side);
            Assert.AreEqual(new HexCoord(1, 1), simulation.Units[0].CurrentHex);
            Assert.AreEqual(77, simulation.Units[1].UnitId);
            Assert.AreSame(enemyDefinition, simulation.Units[1].Definition);
            Assert.AreEqual(BattleSide.Enemy, simulation.Units[1].Side);
            Assert.AreEqual(new HexCoord(3, 4), simulation.Units[1].CurrentHex);
            Assert.AreEqual(2, simulation.Tuning.MovementStepsPerTick);
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

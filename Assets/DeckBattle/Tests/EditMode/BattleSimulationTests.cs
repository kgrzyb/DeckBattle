using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleSimulationTests
    {
        [Test]
        public void Create_InitializesUnitRuntimeStateWithoutScene()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition playerDefinition = TestDefinitions.CreateUnit("player-swordsman", 1);
            UnitDefinition enemyDefinition = TestDefinitions.CreateUnit("enemy-guard", 1);

            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(7, playerDefinition, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(11, enemyDefinition, BattleSide.Enemy, new HexCoord(3, 4))
                });

            Assert.AreSame(board, simulation.Board);
            Assert.AreEqual(2, simulation.Units.Count);

            UnitRuntimeState player = simulation.Units[0];
            Assert.AreEqual(7, player.UnitId);
            Assert.AreEqual(UnitCombatSpec.FromDefinition(playerDefinition).DefinitionId, player.CombatSpec.DefinitionId);
            Assert.AreEqual(BattleSide.Player, player.Side);
            Assert.AreEqual(new HexCoord(1, 1), player.CurrentHex);
            Assert.AreEqual(playerDefinition.MaxHp, player.CurrentHp);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, player.TargetUnitId);
            Assert.That(player.NextAttackTime, Is.EqualTo(playerDefinition.AttackCooldown).Within(0.000001d));
            Assert.AreEqual(0, player.CurrentMana);
            Assert.IsTrue(player.IsAlive);

            UnitRuntimeState enemy;
            Assert.IsTrue(simulation.TryGetUnitAt(new HexCoord(3, 4), out enemy));
            Assert.AreEqual(11, enemy.UnitId);
            Assert.AreEqual(BattleSide.Enemy, enemy.Side);
        }

        [Test]
        public void Create_DetectsDuplicateUnitIds()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);

            Assert.Throws<ArgumentException>(
                () => BattleSimulation.Create(
                    board,
                    new[]
                    {
                        new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(1, 1)),
                        new UnitSpawnData(1, definition, BattleSide.Enemy, new HexCoord(2, 2))
                    }));
        }

        [Test]
        public void Create_DetectsNonPositiveUnitId()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);

            Assert.Throws<ArgumentException>(
                () => BattleSimulation.Create(
                    board,
                    new[]
                    {
                        new UnitSpawnData(0, definition, BattleSide.Player, new HexCoord(1, 1))
                    }));
        }

        [Test]
        public void Create_DetectsDuplicateStartingHexes()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);

            Assert.Throws<ArgumentException>(
                () => BattleSimulation.Create(
                    board,
                    new[]
                    {
                        new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(2, 2)),
                        new UnitSpawnData(2, definition, BattleSide.Enemy, new HexCoord(2, 2))
                    }));
        }

        [Test]
        public void Create_DetectsInvalidStartingHex()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);

            Assert.Throws<ArgumentException>(
                () => BattleSimulation.Create(
                    board,
                    new[]
                    {
                        new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(5, 0))
                    }));
        }

        [Test]
        public void Create_DetectsBlockedStartingHex()
        {
            var board = new HexBoard(5, 6, 1f);
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            board.SetWalkable(new HexCoord(1, 1), false);

            Assert.Throws<ArgumentException>(
                () => BattleSimulation.Create(
                    board,
                    new[]
                    {
                        new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(1, 1))
                    }));
        }

        [Test]
        public void UnitRuntimeState_TracksTargetCooldownAndResetState()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            var unit = new UnitRuntimeState(1, definition, BattleSide.Player, new HexCoord(0, 0));
            var target = new UnitRuntimeState(2, definition, BattleSide.Enemy, new HexCoord(1, 0));
            var replacementTarget = new UnitRuntimeState(3, definition, BattleSide.Enemy, new HexCoord(1, 1));

            unit.CurrentHp = 1;
            unit.CurrentHex = new HexCoord(2, 2);
            unit.NextAttackTime = 0.5d;
            unit.CurrentMana = 25;
            unit.SetTarget(target);

            Assert.AreEqual(2, unit.TargetUnitId);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, unit.EngagedTargetUnitId);
            Assert.AreEqual(0, unit.PursuitStepsUsed);

            unit.MarkTargetEngaged(target.UnitId);
            unit.RecordPursuitStep(target.UnitId);

            Assert.AreEqual(target.UnitId, unit.EngagedTargetUnitId);
            Assert.AreEqual(1, unit.PursuitStepsUsed);
            Assert.IsTrue(unit.CanPursueTarget(target.UnitId, 2));

            unit.SetTarget(target);

            Assert.AreEqual(target.UnitId, unit.EngagedTargetUnitId);
            Assert.AreEqual(1, unit.PursuitStepsUsed);

            unit.SetTarget(replacementTarget);

            Assert.AreEqual(replacementTarget.UnitId, unit.TargetUnitId);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, unit.EngagedTargetUnitId);
            Assert.AreEqual(0, unit.PursuitStepsUsed);

            unit.ResetForBattle(new HexCoord(0, 1));

            Assert.AreEqual(new HexCoord(0, 1), unit.CurrentHex);
            Assert.AreEqual(definition.MaxHp, unit.CurrentHp);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, unit.TargetUnitId);
            Assert.AreEqual(UnitRuntimeState.NoTargetUnitId, unit.EngagedTargetUnitId);
            Assert.AreEqual(0, unit.PursuitStepsUsed);
            Assert.IsTrue(double.IsPositiveInfinity(unit.NextAttackTime));
            Assert.AreEqual(0, unit.CurrentMana);
            Assert.IsTrue(unit.IsAlive);
        }

        [Test]
        public void Create_AllowsEmptySimulation()
        {
            var board = new HexBoard(5, 6, 1f);

            BattleSimulation simulation = BattleSimulation.Create(board, new List<UnitSpawnData>());

            Assert.AreEqual(0, simulation.Units.Count);
        }

        [Test]
        public void StartUnitMovement_RejectsNonAdjacentDestination()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, definition, BattleSide.Enemy, new HexCoord(4, 5))
                });

            Assert.Throws<ArgumentException>(
                () => simulation.StartUnitMovement(simulation.Units[0], new HexCoord(2, 0)));
        }

        [Test]
        public void StartUnitMovement_RejectsAnotherMovingUnitDestination()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, definition, BattleSide.Player, new HexCoord(2, 0)),
                    new UnitSpawnData(3, definition, BattleSide.Enemy, new HexCoord(4, 5))
                });
            simulation.StartUnitMovement(simulation.Units[0], new HexCoord(1, 0));

            Assert.Throws<ArgumentException>(
                () => simulation.StartUnitMovement(simulation.Units[1], new HexCoord(1, 0)));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleCombatRunnerTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>(4);

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Advance_StopsAtConfiguredTickLimit()
        {
            BattleCombatRunner runner = CreateRunner();
            BattleSimulation simulation = CreateUnresolvedSimulation();

            runner.StartCombat(simulation, 0.25f, 3, 8);
            runner.Advance(0.75f);

            Assert.IsFalse(runner.IsRunning);
            Assert.IsNotNull(runner.Result);
            Assert.AreEqual(3, runner.Result.Ticks);
            Assert.IsTrue(runner.Result.MaxTicksReached);
            Assert.AreEqual(CombatEndReason.MaxTicksReached, runner.Result.EndReason);
        }

        [Test]
        public void Advance_RespectsMaxTicksPerFrame()
        {
            BattleCombatRunner runner = CreateRunner();
            BattleSimulation simulation = CreateUnresolvedSimulation();
            int processedTicks = 0;
            runner.TickProcessed += (result, events) => processedTicks++;

            runner.StartCombat(simulation, 0.25f, 8, 2);
            runner.Advance(1f);

            Assert.IsTrue(runner.IsRunning);
            Assert.AreEqual(2, processedTicks);
            Assert.IsNull(runner.Result);
        }

        [Test]
        public void Advance_WhenBattleEnds_RaisesCompletedWithResolution()
        {
            BattleCombatRunner runner = CreateRunner();
            BattleSimulation simulation = CreateDecisiveSimulation();
            BattleRunResult completed = null;
            runner.Completed += result => completed = result;

            runner.StartCombat(simulation, 1f, 8, 8);
            runner.Advance(8f);

            Assert.IsFalse(runner.IsRunning);
            Assert.AreSame(completed, runner.Result);
            Assert.IsFalse(completed.MaxTicksReached);
            Assert.AreEqual(CombatEndReason.OneSideDefeated, completed.EndReason);
            Assert.IsTrue(completed.LastTickResult.HasWinner);
            Assert.AreEqual(BattleSide.Player, completed.LastTickResult.Winner);
        }

        [Test]
        public void StopCombat_AllowsCleanRestart()
        {
            BattleCombatRunner runner = CreateRunner();
            runner.StartCombat(CreateUnresolvedSimulation(), 0.25f, 8, 8);
            runner.Advance(0.25f);

            runner.StopCombat();

            Assert.IsFalse(runner.IsRunning);
            Assert.IsNull(runner.Simulation);
            Assert.IsNull(runner.Result);

            int processedTicks = 0;
            runner.TickProcessed += (result, events) => processedTicks++;
            runner.StartCombat(CreateUnresolvedSimulation(), 0.25f, 8, 8);
            runner.Advance(0.25f);

            Assert.IsTrue(runner.IsRunning);
            Assert.AreEqual(1, processedTicks);
        }

        [Test]
        public void Advance_AfterCompletion_DoesNotProcessMoreTicks()
        {
            BattleCombatRunner runner = CreateRunner();
            int processedTicks = 0;
            runner.TickProcessed += (result, events) => processedTicks++;

            runner.StartCombat(CreateDecisiveSimulation(), 1f, 8, 8);
            runner.Advance(8f);
            int ticksAtCompletion = processedTicks;
            runner.Advance(8f);

            Assert.IsFalse(runner.IsRunning);
            Assert.AreEqual(ticksAtCompletion, processedTicks);
        }

        private BattleCombatRunner CreateRunner()
        {
            var gameObject = new GameObject("BattleCombatRunnerTests");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<BattleCombatRunner>();
        }

        private static BattleSimulation CreateUnresolvedSimulation()
        {
            UnitDefinition player = TestDefinitions.CreateUnit("runner-player", 1);
            player.Attack = 1;
            player.AttackRange = 1;
            player.AttacksPerSecond = 0.1f;

            UnitDefinition enemy = TestDefinitions.CreateUnit("runner-enemy", 1);
            enemy.Attack = 1;
            enemy.AttackRange = 1;
            enemy.AttacksPerSecond = 0.1f;

            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(4, 5))
                });
        }

        private static BattleSimulation CreateDecisiveSimulation()
        {
            UnitDefinition player = TestDefinitions.CreateUnit("runner-player", 1);
            player.Attack = 5;
            player.AttackRange = 3;
            player.AttacksPerSecond = 1f;
            player.AttackWindupPercent = 0.5f;

            UnitDefinition enemy = TestDefinitions.CreateUnit("runner-enemy", 1);
            enemy.MaxHp = 3;
            enemy.Attack = 0;
            enemy.AttackRange = 1;
            enemy.AttacksPerSecond = 1f;

            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                });
        }
    }
}

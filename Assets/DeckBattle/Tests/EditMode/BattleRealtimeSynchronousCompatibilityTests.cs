using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleRealtimeSynchronousCompatibilityTests
    {
        private const float TickDuration = 0.25f;
        private const int MaxTicks = 64;
        private const int RandomSeed = 5729;

        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void RealtimeAndSynchronousCombat_WithSameSeedAndSpawns_ProduceTheSameOutcome()
        {
            BattleState realtimeState = CreateCombatState();
            BattleState synchronousState = CreateCombatState();
            BattleRuntimeTuning tuning = BattleRuntimeTuning.Default;
            BattleSimulation realtimeSimulation = CreateSimulation(realtimeState, tuning, RandomSeed);
            BattleSimulation synchronousSimulation = CreateSimulation(synchronousState, tuning, RandomSeed);

            CombatSimulationResult realtimeResult = RunRealtimeCombat(
                realtimeState,
                realtimeSimulation,
                new BattleTickLoop(realtimeSimulation, TickDuration),
                MaxTicks,
                new BattleEventQueue());
            CombatSimulationResult synchronousResult = BattleSimulationCombatService.RunToResolution(
                synchronousState,
                synchronousSimulation,
                new BattleTickLoop(synchronousSimulation, TickDuration),
                MaxTicks,
                new BattleEventQueue());

            AssertResultsEqual(realtimeResult, synchronousResult);
            AssertSimulationsEqual(realtimeSimulation, synchronousSimulation);
            AssertRuntimeUnitsEqual(realtimeState, synchronousState);
        }

        private static CombatSimulationResult RunRealtimeCombat(
            BattleState state,
            BattleSimulation simulation,
            BattleTickLoop tickLoop,
            int maxTicks,
            BattleEventQueue eventQueue)
        {
            int ticks = 0;
            while (state.Phase == BattlePhase.Combat && ticks < maxTicks)
            {
                BattleTickResult tickResult = tickLoop.Tick(eventQueue);
                ticks++;
                if (tickResult.BattleEnded)
                {
                    state.Phase = BattlePhase.RoundResolution;
                    BattleSimulationResultApplier.Apply(state, simulation);
                    return BattleSimulationCombatService.CreateCombatResult(tickResult, ticks);
                }
            }

            state.Phase = BattlePhase.RoundResolution;
            BattleSimulationResultApplier.Apply(state, simulation);
            return CombatSimulationResult.MaxTicksReached(ticks);
        }

        private static BattleState CreateCombatState()
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            config.StartingHandSize = 0;
            BattleState state = BattleState.Create(
                config,
                new List<CardDefinition>(),
                new List<CardDefinition>(),
                RandomSeed);
            state.Phase = BattlePhase.Combat;

            UnitDefinition player = TestDefinitions.CreateUnit("compatibility-player", 1);
            player.MaxHp = 10;
            player.Attack = 3;
            player.AttackRange = 2;
            player.AttacksPerSecond = 2f;
            player.AttackWindupPercent = 0.5f;
            player.CritChance = 35f;

            UnitDefinition enemy = TestDefinitions.CreateUnit("compatibility-enemy", 1);
            enemy.MaxHp = 7;
            enemy.Attack = 1;
            enemy.AttackRange = 1;
            enemy.AttacksPerSecond = 2f;
            enemy.AttackWindupPercent = 0.5f;

            state.Player.Units.Add(new RuntimeUnit(1, player, BattleSide.Player, new HexCoord(0, 0)));
            state.Enemy.Units.Add(new RuntimeUnit(2, enemy, BattleSide.Enemy, new HexCoord(3, 0)));
            return state;
        }

        private static BattleSimulation CreateSimulation(BattleState state, BattleRuntimeTuning tuning, int randomSeed)
        {
            var spawns = new List<UnitSpawnData>
            {
                new UnitSpawnData(1, state.Player.Units[0].Definition, BattleSide.Player, new HexCoord(0, 0)),
                new UnitSpawnData(2, state.Enemy.Units[0].Definition, BattleSide.Enemy, new HexCoord(3, 0))
            };
            return BattleSimulation.Create(new HexBoard(5, 6, 1f), spawns, tuning, randomSeed);
        }

        private static void AssertResultsEqual(CombatSimulationResult expected, CombatSimulationResult actual)
        {
            Assert.AreEqual(expected.Ticks, actual.Ticks);
            Assert.AreEqual(expected.CombatEnded, actual.CombatEnded);
            Assert.AreEqual(expected.HasWinner, actual.HasWinner);
            Assert.AreEqual(expected.Winner, actual.Winner);
            Assert.AreEqual(expected.EndReason, actual.EndReason);
        }

        private static void AssertSimulationsEqual(BattleSimulation expected, BattleSimulation actual)
        {
            Assert.That(actual.ElapsedTime, Is.EqualTo(expected.ElapsedTime).Within(0.000001d));
            Assert.AreEqual(expected.IsBattleEnded, actual.IsBattleEnded);
            Assert.AreEqual(expected.HasWinner, actual.HasWinner);
            Assert.AreEqual(expected.Winner, actual.Winner);
            Assert.AreEqual(expected.Units.Count, actual.Units.Count);

            for (int i = 0; i < expected.Units.Count; i++)
            {
                AssertUnitStateEqual(expected.Units[i], actual.Units[i]);
            }
        }

        private static void AssertRuntimeUnitsEqual(BattleState expected, BattleState actual)
        {
            AssertPlayerUnitsEqual(expected.Player, actual.Player);
            AssertPlayerUnitsEqual(expected.Enemy, actual.Enemy);
        }

        private static void AssertPlayerUnitsEqual(PlayerBattleState expected, PlayerBattleState actual)
        {
            Assert.AreEqual(expected.Units.Count, actual.Units.Count);
            for (int i = 0; i < expected.Units.Count; i++)
            {
                RuntimeUnit expectedUnit = expected.Units[i];
                RuntimeUnit actualUnit = actual.Units[i];
                Assert.AreEqual(expectedUnit.RuntimeId, actualUnit.RuntimeId);
                Assert.AreEqual(expectedUnit.CurrentHp, actualUnit.CurrentHp);
                Assert.AreEqual(expectedUnit.BattleCoord, actualUnit.BattleCoord);
                Assert.AreEqual(expectedUnit.IsDefeated, actualUnit.IsDefeated);
            }
        }

        private static void AssertUnitStateEqual(UnitRuntimeState expected, UnitRuntimeState actual)
        {
            Assert.AreEqual(expected.UnitId, actual.UnitId);
            Assert.AreEqual(expected.Side, actual.Side);
            Assert.AreEqual(expected.CurrentHp, actual.CurrentHp);
            Assert.AreEqual(expected.CurrentMana, actual.CurrentMana);
            Assert.AreEqual(expected.PassiveManaRemainder, actual.PassiveManaRemainder);
            Assert.AreEqual(expected.CurrentHex, actual.CurrentHex);
            Assert.AreEqual(expected.PreviousHex, actual.PreviousHex);
            Assert.AreEqual(expected.TargetUnitId, actual.TargetUnitId);
            Assert.AreEqual(expected.EngagedTargetUnitId, actual.EngagedTargetUnitId);
            Assert.AreEqual(expected.PursuitStepsUsed, actual.PursuitStepsUsed);
            Assert.AreEqual(expected.IsMoving, actual.IsMoving);
            Assert.AreEqual(expected.MovementDestination, actual.MovementDestination);
            Assert.That(actual.MovementTimeRemaining, Is.EqualTo(expected.MovementTimeRemaining).Within(0.000001f));
            Assert.AreEqual(expected.IsDefeated, actual.IsDefeated);
            Assert.AreEqual(expected.AttackPhase, actual.AttackPhase);
            Assert.AreEqual(expected.SpecialPhase, actual.SpecialPhase);
            Assert.AreEqual(expected.Statuses.Count, actual.Statuses.Count);
        }
    }
}

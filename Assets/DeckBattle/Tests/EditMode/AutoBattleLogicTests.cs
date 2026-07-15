using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class AutoBattleLogicTests
    {
        [Test]
        public void FindNearestTarget_UsesDistanceHpAndRuntimeIdTieBreakers()
        {
            var board = new HexBoard(5, 6, 1f);
            RuntimeUnit attacker = CreateRuntimeUnit(10, BattleSide.Player, UnitType.Melee, new HexCoord(0, 0), 5, 1, 1, 1);
            PlayerBattleState enemy = new PlayerBattleState(BattleSide.Enemy, 30, 3, 3);

            RuntimeUnit farLowHp = CreateRuntimeUnit(1, BattleSide.Enemy, UnitType.Melee, new HexCoord(4, 5), 1, 1, 1, 1);
            RuntimeUnit sameDistanceHigherHp = CreateRuntimeUnit(2, BattleSide.Enemy, UnitType.Melee, new HexCoord(0, 2), 5, 1, 1, 1);
            RuntimeUnit sameDistanceLowerRuntimeId = CreateRuntimeUnit(3, BattleSide.Enemy, UnitType.Melee, new HexCoord(1, 1), 2, 1, 1, 1);
            RuntimeUnit sameDistanceHigherRuntimeId = CreateRuntimeUnit(4, BattleSide.Enemy, UnitType.Melee, new HexCoord(2, 0), 2, 1, 1, 1);
            enemy.Units.Add(farLowHp);
            enemy.Units.Add(sameDistanceHigherHp);
            enemy.Units.Add(sameDistanceHigherRuntimeId);
            enemy.Units.Add(sameDistanceLowerRuntimeId);

            RuntimeUnit target = TargetingService.FindNearestTarget(board, attacker, enemy);

            Assert.AreSame(sameDistanceLowerRuntimeId, target);
        }

        [Test]
        public void MoveTowardsTarget_MeleeSelectsNearestTargetAndApproaches()
        {
            var board = new HexBoard(5, 6, 1f);
            RuntimeUnit melee = CreateRuntimeUnit(1, BattleSide.Player, UnitType.Melee, new HexCoord(2, 0), 5, 1, 1, 1);
            PlayerBattleState enemy = new PlayerBattleState(BattleSide.Enemy, 30, 3, 3);
            RuntimeUnit nearest = CreateRuntimeUnit(2, BattleSide.Enemy, UnitType.Melee, new HexCoord(2, 3), 5, 1, 1, 1);
            RuntimeUnit farther = CreateRuntimeUnit(3, BattleSide.Enemy, UnitType.Melee, new HexCoord(4, 5), 5, 1, 1, 1);
            enemy.Units.Add(farther);
            enemy.Units.Add(nearest);
            var allUnits = new List<RuntimeUnit> { melee, nearest, farther };

            RuntimeUnit target = TargetingService.FindNearestTarget(board, melee, enemy);
            int beforeDistance = board.Distance(melee.BattleCoord, target.BattleCoord);
            bool moved = MovementService.MoveTowardsTarget(board, melee, target, allUnits);

            Assert.AreSame(nearest, target);
            Assert.IsTrue(moved);
            Assert.AreEqual(new HexCoord(2, 1), melee.BattleCoord);
            Assert.Less(board.Distance(melee.BattleCoord, target.BattleCoord), beforeDistance);
        }

        [Test]
        public void MoveTowardsTarget_DoesNotEnterOccupiedHex()
        {
            var board = new HexBoard(5, 6, 1f);
            RuntimeUnit melee = CreateRuntimeUnit(1, BattleSide.Player, UnitType.Melee, new HexCoord(2, 0), 5, 1, 1, 1);
            RuntimeUnit blocker = CreateRuntimeUnit(2, BattleSide.Player, UnitType.Melee, new HexCoord(2, 1), 5, 1, 1, 1);
            RuntimeUnit target = CreateRuntimeUnit(3, BattleSide.Enemy, UnitType.Melee, new HexCoord(3, 2), 5, 1, 1, 1);
            var allUnits = new List<RuntimeUnit> { melee, blocker, target };

            bool moved = MovementService.MoveTowardsTarget(board, melee, target, allUnits);

            Assert.IsTrue(moved);
            Assert.AreNotEqual(blocker.BattleCoord, melee.BattleCoord);
            Assert.AreEqual(new HexCoord(3, 0), melee.BattleCoord);
        }

        [Test]
        public void Simulate_RangeAttacksWithoutMoving_WhenTargetIsInRange()
        {
            RuntimeUnit ranged = CreateRuntimeUnit(1, BattleSide.Player, UnitType.Range, new HexCoord(2, 0), 5, 2, 3, 1);
            RuntimeUnit enemy = CreateRuntimeUnit(2, BattleSide.Enemy, UnitType.Melee, new HexCoord(2, 2), 5, 0, 1, 0);
            BattleState state = CreateCombatState(ranged, enemy);

            CombatSimulator.Simulate(state, 1f, 1);

            Assert.AreEqual(new HexCoord(2, 0), ranged.BattleCoord);
            Assert.AreEqual(3, enemy.CurrentHp);
            Assert.IsFalse(enemy.IsDefeated);
        }

        [Test]
        public void Simulate_DefeatedUnitStopsFighting()
        {
            RuntimeUnit player = CreateRuntimeUnit(1, BattleSide.Player, UnitType.Melee, new HexCoord(2, 0), 5, 5, 1, 0);
            RuntimeUnit enemy = CreateRuntimeUnit(2, BattleSide.Enemy, UnitType.Melee, new HexCoord(2, 1), 3, 5, 1, 0);
            BattleState state = CreateCombatState(player, enemy);

            CombatSimulationResult result = CombatSimulator.Simulate(state, 1f, 10);

            Assert.IsTrue(result.CombatEnded);
            Assert.IsTrue(result.HasWinner);
            Assert.AreEqual(BattleSide.Player, result.Winner);
            Assert.IsTrue(enemy.IsDefeated);
            Assert.AreEqual(5, player.CurrentHp);
            Assert.AreEqual(BattlePhase.RoundResolution, state.Phase);
        }

        [Test]
        public void Simulate_CombatEndsDeterministically()
        {
            BattleState first = CreateDeterministicDuelState();
            BattleState second = CreateDeterministicDuelState();

            CombatSimulationResult firstResult = CombatSimulator.Simulate(first, 1f, 20);
            CombatSimulationResult secondResult = CombatSimulator.Simulate(second, 1f, 20);

            Assert.IsTrue(firstResult.CombatEnded);
            Assert.IsTrue(secondResult.CombatEnded);
            Assert.AreEqual(firstResult.Ticks, secondResult.Ticks);
            Assert.AreEqual(firstResult.HasWinner, secondResult.HasWinner);
            Assert.AreEqual(firstResult.Winner, secondResult.Winner);
            Assert.AreEqual(first.Player.Units[0].CurrentHp, second.Player.Units[0].CurrentHp);
            Assert.AreEqual(first.Enemy.Units[0].CurrentHp, second.Enemy.Units[0].CurrentHp);
            Assert.AreEqual(first.Player.Units[0].IsDefeated, second.Player.Units[0].IsDefeated);
            Assert.AreEqual(first.Enemy.Units[0].IsDefeated, second.Enemy.Units[0].IsDefeated);
            Assert.AreEqual(BattlePhase.RoundResolution, first.Phase);
            Assert.AreEqual(BattleSide.Player, firstResult.Winner);
        }

        [Test]
        public void Simulate_WhenMaxTicksReached_EndsInRoundResolutionWithoutWinner()
        {
            RuntimeUnit player = CreateRuntimeUnit(1, BattleSide.Player, UnitType.Melee, new HexCoord(0, 0), 5, 0, 1, 0);
            RuntimeUnit enemy = CreateRuntimeUnit(2, BattleSide.Enemy, UnitType.Melee, new HexCoord(4, 5), 5, 0, 1, 0);
            BattleState state = CreateCombatState(player, enemy);

            CombatSimulationResult result = CombatSimulator.Simulate(state, 1f, 2);

            Assert.IsTrue(result.CombatEnded);
            Assert.IsFalse(result.HasWinner);
            Assert.AreEqual(CombatEndReason.MaxTicksReached, result.EndReason);
            Assert.AreEqual(2, result.Ticks);
            Assert.AreEqual(BattlePhase.RoundResolution, state.Phase);
            Assert.IsTrue(player.IsAlive);
            Assert.IsTrue(enemy.IsAlive);
        }

        private static BattleState CreateDeterministicDuelState()
        {
            RuntimeUnit player = CreateRuntimeUnit(1, BattleSide.Player, UnitType.Melee, new HexCoord(2, 0), 5, 2, 1, 0);
            RuntimeUnit enemy = CreateRuntimeUnit(2, BattleSide.Enemy, UnitType.Melee, new HexCoord(2, 1), 6, 1, 1, 0);
            return CreateCombatState(player, enemy);
        }

        private static BattleState CreateCombatState(RuntimeUnit playerUnit, RuntimeUnit enemyUnit)
        {
            BattleConfig config = TestDefinitions.CreateConfig();
            BattleState state = BattleState.Create(config, CreateDeck("player"), CreateDeck("enemy"), 42);
            state.Player.Units.Clear();
            state.Enemy.Units.Clear();
            state.Player.Units.Add(playerUnit);
            state.Enemy.Units.Add(enemyUnit);
            state.Player.IsReady = true;
            state.Enemy.IsReady = true;
            state.Phase = BattlePhase.Combat;
            return state;
        }

        private static RuntimeUnit CreateRuntimeUnit(int runtimeId, BattleSide side, UnitType unitType, HexCoord coord, int hp, int attack, int attackRange, int moveRange)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(side + "-" + runtimeId, 1, unitType);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = attackRange;
            definition.MoveRange = moveRange;
            definition.AttackCooldown = 1f;
            return new RuntimeUnit(runtimeId, definition, side, coord);
        }

        private static List<UnitDefinition> CreateDeck(string prefix)
        {
            return new List<UnitDefinition>
            {
                TestDefinitions.CreateUnit(prefix + "-guard", 1),
                TestDefinitions.CreateUnit(prefix + "-swordsman", 1),
                TestDefinitions.CreateUnit(prefix + "-archer", 1, UnitType.Range),
                TestDefinitions.CreateUnit(prefix + "-scout", 1)
            };
        }
    }
}

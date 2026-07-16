using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleStateCombatTickLoopTests
    {
        [Test]
        public void Tick_OutOfRangeUnits_MoveAndEmitEvents()
        {
            BattleState state = CreateCombatState(
                CreateUnit("player", 5, 1, 1, 1),
                new HexCoord(0, 0),
                CreateUnit("enemy", 5, 1, 1, 1),
                new HexCoord(3, 0));
            var loop = new BattleStateCombatTickLoop(state, 1f, 10);
            var events = new BattleEventQueue();

            CombatSimulationResult result = loop.Tick(events);

            Assert.IsNull(result);
            Assert.AreEqual(BattlePhase.Combat, state.Phase);
            Assert.AreEqual(new HexCoord(1, 0), state.Player.Units[0].BattleCoord);
            AssertEventTypeExists(events, BattleEventType.UnitMoved);
        }

        [Test]
        public void Tick_KillingAttackEndsCombatAndEmitsPresentationEvents()
        {
            BattleState state = CreateCombatState(
                CreateUnit("player-ranged", 5, 4, 3, 1),
                new HexCoord(0, 0),
                CreateUnit("enemy", 3, 1, 1, 1),
                new HexCoord(2, 0));
            var loop = new BattleStateCombatTickLoop(state, 1f, 10);
            var events = new BattleEventQueue();

            CombatSimulationResult result = loop.Tick(events);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.CombatEnded);
            Assert.IsTrue(result.HasWinner);
            Assert.AreEqual(BattleSide.Player, result.Winner);
            Assert.AreEqual(BattlePhase.RoundResolution, state.Phase);
            Assert.IsTrue(state.Enemy.Units[0].IsDefeated);
            AssertEventTypeExists(events, BattleEventType.UnitAttackStarted);
            AssertEventTypeExists(events, BattleEventType.UnitDamaged);
            AssertEventTypeExists(events, BattleEventType.UnitDied);
        }

        private static BattleState CreateCombatState(
            UnitDefinition playerDefinition,
            HexCoord playerHex,
            UnitDefinition enemyDefinition,
            HexCoord enemyHex)
        {
            BattleState state = BattleState.Create(
                TestDefinitions.CreateConfig(),
                new UnitDefinition[0],
                new UnitDefinition[0],
                123);
            state.Phase = BattlePhase.Combat;

            state.Player.Units.Add(new RuntimeUnit(state.AllocateRuntimeUnitId(), playerDefinition, BattleSide.Player, playerHex));
            state.Enemy.Units.Add(new RuntimeUnit(state.AllocateRuntimeUnitId(), enemyDefinition, BattleSide.Enemy, enemyHex));
            return state;
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, int moveRange)
        {
            UnitDefinition unit = TestDefinitions.CreateUnit(unitId, 1);
            unit.MaxHp = hp;
            unit.Attack = attack;
            unit.AttackRange = attackRange;
            unit.MoveRange = moveRange;
            unit.AttackCooldown = 1f;
            return unit;
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

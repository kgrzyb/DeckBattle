using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleTickLoopTests
    {
        [Test]
        public void Tick_OneVsOneMelee_MovesThenEndsDeterministically()
        {
            BattleSimulation first = CreateMeleeDuel();
            BattleSimulation second = CreateMeleeDuel();
            BattleTickLoop firstLoop = new BattleTickLoop(first, 1f);
            BattleTickLoop secondLoop = new BattleTickLoop(second, 1f);
            var firstEvents = new BattleEventQueue();
            var secondEvents = new BattleEventQueue();

            BattleTickResult firstTick = firstLoop.Tick(first, firstEvents);
            BattleTickResult secondTick = secondLoop.Tick(second, secondEvents);

            Assert.IsFalse(firstTick.BattleEnded);
            Assert.AreEqual(0, firstTick.Attacks);
            Assert.AreEqual(2, firstTick.Moves);
            Assert.AreEqual(new HexCoord(1, 0), first.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 1), first.Units[1].CurrentHex);
            Assert.AreEqual(first.Units[0].CurrentHex, second.Units[0].CurrentHex);
            Assert.AreEqual(first.Units[1].CurrentHex, second.Units[1].CurrentHex);
            Assert.AreEqual(2, firstEvents.Count);
            Assert.AreEqual(BattleEventType.UnitMoved, firstEvents[0].Type);

            BattleTickResult endTick = firstLoop.Tick(first, firstEvents);

            Assert.IsTrue(endTick.BattleEnded);
            Assert.IsTrue(endTick.HasWinner);
            Assert.AreEqual(BattleSide.Player, endTick.Winner);
            Assert.IsTrue(first.IsBattleEnded);
            Assert.IsTrue(first.Units[1].IsDefeated);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitAttackStarted);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitDamaged);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitDied);
            AssertEventTypeExists(firstEvents, BattleEventType.BattleEnded);
        }

        [Test]
        public void Tick_OneVsOneRanged_AttacksWithoutMoving()
        {
            UnitDefinition player = CreateUnit("player-ranged", 5, 4, 3, 1f);
            UnitDefinition enemy = CreateUnit("enemy-melee", 3, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(enemy, BattleSide.Enemy, new HexCoord(2, 1))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(simulation, events);

            Assert.IsTrue(result.BattleEnded);
            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(0, result.Moves);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
            AssertEventTypeExists(events, BattleEventType.BattleEnded);
        }

        [Test]
        public void Tick_MultipleUnits_ProducesStableBattleOutcome()
        {
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(CreateUnit("player-front", 6, 2, 1, 1f), BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(CreateUnit("player-ranged", 4, 1, 3, 1f), BattleSide.Player, new HexCoord(0, 1)),
                    new UnitSpawnData(CreateUnit("enemy-front", 3, 1, 1, 1f), BattleSide.Enemy, new HexCoord(2, 1))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = RunUntilEnded(simulation, loop, events, 10);

            Assert.IsTrue(result.BattleEnded);
            Assert.IsTrue(result.HasWinner);
            Assert.AreEqual(BattleSide.Player, result.Winner);
            Assert.IsTrue(simulation.Units[2].IsDefeated);
            Assert.IsTrue(simulation.Units[0].IsAlive || simulation.Units[1].IsAlive);
        }

        [Test]
        public void Tick_DoesNotEmitBattleEndedAgain_AfterBattleAlreadyEnded()
        {
            UnitDefinition player = CreateUnit("player-ranged", 5, 4, 3, 1f);
            UnitDefinition enemy = CreateUnit("enemy-melee", 3, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(enemy, BattleSide.Enemy, new HexCoord(2, 1))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            loop.Tick(simulation, events);
            BattleTickResult second = loop.Tick(simulation, events);

            Assert.IsTrue(second.BattleEnded);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Tick_EmptyOrMutualDefeatBattle_EndsWithoutWinner()
        {
            BattleSimulation simulation = BattleSimulation.Create(new HexBoard(5, 6, 1f), new UnitSpawnData[0]);
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(simulation, events);

            Assert.IsTrue(result.BattleEnded);
            Assert.IsFalse(result.HasWinner);
            Assert.IsTrue(simulation.IsBattleEnded);
            Assert.AreEqual(BattleEventType.BattleEnded, events[0].Type);
            Assert.IsFalse(events[0].HasWinner);
        }

        private static BattleSimulation CreateMeleeDuel()
        {
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(CreateUnit("player-melee", 5, 5, 1, 1f), BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(CreateUnit("enemy-melee", 3, 1, 1, 1f), BattleSide.Enemy, new HexCoord(2, 0))
                });
        }

        private static BattleTickResult RunUntilEnded(
            BattleSimulation simulation,
            BattleTickLoop loop,
            BattleEventQueue events,
            int maxTicks)
        {
            BattleTickResult result = default;
            for (int i = 0; i < maxTicks; i++)
            {
                result = loop.Tick(simulation, events);
                if (result.BattleEnded)
                {
                    return result;
                }
            }

            Assert.Fail("Battle did not end within expected ticks.");
            return result;
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

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, float attackCooldown)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = attackRange;
            definition.AttackCooldown = attackCooldown;
            return definition;
        }
    }
}

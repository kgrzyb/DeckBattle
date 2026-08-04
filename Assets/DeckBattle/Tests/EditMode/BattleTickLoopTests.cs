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

            BattleTickResult firstTick = firstLoop.Tick(firstEvents);
            BattleTickResult secondTick = secondLoop.Tick(secondEvents);

            Assert.IsFalse(firstTick.BattleEnded);
            Assert.AreEqual(0, firstTick.Attacks);
            Assert.AreEqual(1, firstTick.Moves);
            Assert.AreEqual(new HexCoord(0, 0), first.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(1, 0), first.Units[0].MovementDestination);
            Assert.AreEqual(new HexCoord(2, 0), first.Units[1].CurrentHex);
            Assert.IsTrue(first.Units[0].IsMoving);
            Assert.AreEqual(first.Units[0].CurrentHex, second.Units[0].CurrentHex);
            Assert.AreEqual(first.Units[1].CurrentHex, second.Units[1].CurrentHex);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitMoved);

            BattleTickResult windupTick = firstLoop.Tick(firstEvents);
            BattleTickResult commitTick = firstLoop.Tick(firstEvents);

            Assert.AreEqual(0, windupTick.Attacks);
            Assert.IsTrue(commitTick.BattleEnded);
            Assert.AreEqual(2, commitTick.Attacks);
            Assert.AreEqual(new HexCoord(1, 0), first.Units[0].CurrentHex);
            Assert.AreEqual(new HexCoord(2, 0), first.Units[1].CurrentHex);
            Assert.IsTrue(first.IsBattleEnded);
            Assert.IsTrue(first.Units[1].IsDefeated);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitAttackStarted);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitDamaged);
            AssertEventTypeExists(firstEvents, BattleEventType.UnitDied);
            AssertEventTypeExists(firstEvents, BattleEventType.BattleEnded);

            BattleTickResult endTick = firstLoop.Tick(firstEvents);
            Assert.IsTrue(endTick.BattleEnded);
            Assert.AreEqual(0, firstEvents.Count);
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
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 1))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult windup = loop.Tick(events);
            BattleTickResult result = loop.Tick(events);

            Assert.AreEqual(0, windup.Attacks);
            Assert.IsTrue(result.BattleEnded);
            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(0, result.Moves);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
            AssertEventTypeExists(events, BattleEventType.BattleEnded);
        }

        [Test]
        public void Tick_MirroredSimulationsEmitIdenticalEventsAndFinalState()
        {
            BattleSimulation first = CreateMeleeDuel();
            BattleSimulation second = CreateMeleeDuel();
            var firstLoop = new BattleTickLoop(first, 0.25f);
            var secondLoop = new BattleTickLoop(second, 0.25f);
            var firstEvents = new BattleEventQueue();
            var secondEvents = new BattleEventQueue();

            for (int tick = 0; tick < 16 && !first.IsBattleEnded; tick++)
            {
                BattleTickResult firstResult = firstLoop.Tick(firstEvents);
                BattleTickResult secondResult = secondLoop.Tick(secondEvents);

                Assert.AreEqual(firstResult.Attacks, secondResult.Attacks);
                Assert.AreEqual(firstResult.Moves, secondResult.Moves);
                Assert.AreEqual(firstEvents.Count, secondEvents.Count);
                for (int eventIndex = 0; eventIndex < firstEvents.Count; eventIndex++)
                {
                    AssertEventsEqual(firstEvents[eventIndex], secondEvents[eventIndex]);
                }
            }

            Assert.IsTrue(first.IsBattleEnded);
            Assert.AreEqual(first.HasWinner, second.HasWinner);
            Assert.AreEqual(first.Winner, second.Winner);
            for (int i = 0; i < first.Units.Count; i++)
            {
                Assert.AreEqual(first.Units[i].CurrentHp, second.Units[i].CurrentHp);
                Assert.AreEqual(first.Units[i].CurrentHex, second.Units[i].CurrentHex);
                Assert.AreEqual(first.Units[i].IsDefeated, second.Units[i].IsDefeated);
            }
        }

        [Test]
        public void Tick_RetargetsToCloserEnemy_WhenCurrentTargetIsReachable()
        {
            UnitDefinition player = CreateUnit("player-melee", 5, 5, 1, 1f);
            UnitDefinition enemy = CreateUnit("enemy-melee", 5, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(4, 0)),
                    new UnitSpawnData(3, enemy, BattleSide.Enemy, new HexCoord(0, 1))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[1].NextAttackTime = 10d;
            simulation.Units[2].NextAttackTime = 10d;
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(events);

            Assert.IsFalse(result.BattleEnded);
            Assert.AreEqual(0, result.Attacks);
            Assert.AreEqual(3, simulation.Units[0].TargetUnitId);
            Assert.AreEqual(5, simulation.Units[2].CurrentHp);
            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.AreEqual(simulation.Units[0].CurrentHex, simulation.Units[0].MovementDestination);
            Assert.IsFalse(simulation.Units[0].IsMoving);
            BattleEvent targetChangedEvent = FindEvent(events, BattleEventType.UnitTargetChanged, 1);
            Assert.AreEqual(3, targetChangedEvent.TargetUnitId);
            Assert.AreEqual(new HexCoord(0, 1), targetChangedEvent.To);
        }

        [Test]
        public void Tick_StationaryUnitEmitsTargetUpdate_WhenCurrentTargetMoves()
        {
            UnitDefinition ranged = CreateUnit("ranged", 20, 1, 5, 100f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, ranged, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, ranged, BattleSide.Enemy, new HexCoord(2, 0))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            loop.Tick(events);
            simulation.MoveUnit(simulation.Units[1], new HexCoord(2, 1));

            loop.Tick(events);

            Assert.AreEqual(new HexCoord(0, 0), simulation.Units[0].CurrentHex);
            Assert.IsFalse(simulation.Units[0].IsMoving);
            BattleEvent targetChangedEvent = FindEvent(events, BattleEventType.UnitTargetChanged, 1);
            Assert.AreEqual(2, targetChangedEvent.TargetUnitId);
            Assert.AreEqual(new HexCoord(2, 1), targetChangedEvent.To);
        }

        [Test]
        public void Tick_MovingUnitRetainsTargetAcrossTransientPathBlockage()
        {
            UnitDefinition melee = CreateUnit("melee", 20, 2, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(3, melee, BattleSide.Player, new HexCoord(1, 0)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(0, 5)),
                    new UnitSpawnData(4, melee, BattleSide.Enemy, new HexCoord(2, 4))
                });
            var loop = new BattleTickLoop(simulation, 0.15f);
            var events = new BattleEventQueue();

            for (int tick = 0; tick < 7; tick++)
            {
                loop.Tick(events);
            }

            UnitRuntimeState enemy = simulation.Units[2];
            Assert.AreEqual(1, enemy.TargetUnitId);
            Assert.AreEqual(new HexCoord(0, 3), enemy.CurrentHex);
            Assert.IsFalse(enemy.IsMoving);
            Assert.AreEqual(enemy.CurrentHex, enemy.MovementDestination);
        }

        [Test]
        public void Tick_CrowdedMeleePursuit_DoesNotOscillateBetweenTwoHexes()
        {
            UnitDefinition melee = CreateUnit("melee", 100, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, melee, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(3, melee, BattleSide.Player, new HexCoord(3, 0)),
                    new UnitSpawnData(5, melee, BattleSide.Player, new HexCoord(2, 2)),
                    new UnitSpawnData(2, melee, BattleSide.Enemy, new HexCoord(4, 5)),
                    new UnitSpawnData(4, melee, BattleSide.Enemy, new HexCoord(0, 5)),
                    new UnitSpawnData(6, melee, BattleSide.Enemy, new HexCoord(1, 3))
                });
            var loop = new BattleTickLoop(simulation, 0.15f);
            var events = new BattleEventQueue();
            HexCoord lastFrom = default;
            HexCoord lastTo = default;
            bool hasPreviousMove = false;
            int consecutiveReversals = 0;

            for (int tick = 0; tick < 12; tick++)
            {
                loop.Tick(events);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    BattleEvent battleEvent = events[eventIndex];
                    if (battleEvent.Type != BattleEventType.UnitMoved || battleEvent.UnitId != 4)
                    {
                        continue;
                    }

                    bool reversedPreviousMove = hasPreviousMove
                        && battleEvent.From == lastTo
                        && battleEvent.To == lastFrom;
                    consecutiveReversals = reversedPreviousMove ? consecutiveReversals + 1 : 0;
                    Assert.Less(consecutiveReversals, 2);
                    hasPreviousMove = true;
                    lastFrom = battleEvent.From;
                    lastTo = battleEvent.To;
                }
            }
        }

        [Test]
        public void Tick_MultipleUnits_ProducesStableBattleOutcome()
        {
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, CreateUnit("player-front", 6, 2, 1, 1f), BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, CreateUnit("player-ranged", 4, 1, 3, 1f), BattleSide.Player, new HexCoord(0, 1)),
                    new UnitSpawnData(3, CreateUnit("enemy-front", 3, 1, 1, 1f), BattleSide.Enemy, new HexCoord(2, 1))
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
        public void Tick_ScoutMirrorWithContestedMovement_WaitsThenResolvesSimultaneously()
        {
            UnitDefinition player = CreateScout("player-scout");
            UnitDefinition enemy = CreateScout("enemy-scout");
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(1, 2)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 3))
                });
            var loop = new BattleTickLoop(simulation, BattleTiming.DefaultCombatTickDuration);
            var events = new BattleEventQueue();

            BattleTickResult result = RunUntilEnded(simulation, loop, events, 30);

            Assert.IsTrue(result.BattleEnded);
            Assert.IsFalse(result.HasWinner);
            Assert.IsTrue(simulation.Units[0].IsDefeated);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
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
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 1))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            loop.Tick(events);
            BattleTickResult fireTick = loop.Tick(events);
            BattleTickResult endedTick = loop.Tick(events);

            Assert.IsTrue(fireTick.BattleEnded);
            Assert.IsTrue(endedTick.BattleEnded);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Tick_AdvancesElapsedTimeOncePerActiveTick()
        {
            BattleSimulation simulation = CreateMeleeDuel();
            var loop = new BattleTickLoop(simulation, 0.35f);
            var events = new BattleEventQueue();

            Assert.That(simulation.ElapsedTime, Is.EqualTo(0d).Within(0.000001d));

            loop.Tick(events);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(0.35d).Within(0.000001d));

            loop.Tick(events);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(0.70d).Within(0.000001d));

            loop.Tick(events);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(1.05d).Within(0.000001d));
        }

        [Test]
        public void Tick_DoesNotAdvanceElapsedTimeAfterBattleEnds()
        {
            UnitDefinition player = CreateUnit("player-ranged", 5, 4, 3, 1f);
            UnitDefinition enemy = CreateUnit("enemy-melee", 3, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 1))
                });
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            loop.Tick(events);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(1d).Within(0.000001d));

            loop.Tick(events);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(2d).Within(0.000001d));

            loop.Tick(events);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(2d).Within(0.000001d));
        }

        [Test]
        public void Tick_EmptyOrMutualDefeatBattle_EndsWithoutWinner()
        {
            BattleSimulation simulation = BattleSimulation.Create(new HexBoard(5, 6, 1f), new UnitSpawnData[0]);
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(events);

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
                    new UnitSpawnData(1, CreateUnit("player-melee", 5, 5, 1, 1f), BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, CreateUnit("enemy-melee", 3, 1, 1, 1f), BattleSide.Enemy, new HexCoord(2, 0))
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
                result = loop.Tick(events);
                if (result.BattleEnded)
                {
                    return result;
                }
            }

            Assert.Fail("Battle did not end within expected ticks.");
            return result;
        }

        private static UnitDefinition CreateScout(string unitId)
        {
            UnitDefinition definition = CreateUnit(unitId, 35, 5, 1, 0.5f);
            definition.ManaThreshold = 100;
            definition.ManaPerAttack = 25;
            definition.ManaPerDamageTaken = 10;
            return definition;
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

        private static BattleEvent FindEvent(BattleEventQueue events, BattleEventType type, int unitId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == type && battleEvent.UnitId == unitId)
                {
                    return battleEvent;
                }
            }

            Assert.Fail("Expected event type was not emitted: " + type);
            return default;
        }

        private static void AssertEventsEqual(BattleEvent first, BattleEvent second)
        {
            Assert.AreEqual(first.Type, second.Type);
            Assert.AreEqual(first.UnitId, second.UnitId);
            Assert.AreEqual(first.TargetUnitId, second.TargetUnitId);
            Assert.AreEqual(first.From, second.From);
            Assert.AreEqual(first.To, second.To);
            Assert.AreEqual(first.Amount, second.Amount);
            Assert.AreEqual(first.RemainingHp, second.RemainingHp);
            Assert.AreEqual(first.CurrentMana, second.CurrentMana);
            Assert.AreEqual(first.SequenceId, second.SequenceId);
            Assert.AreEqual(first.StatusKind, second.StatusKind);
            Assert.AreEqual(first.PresentationId, second.PresentationId);
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, float attackCooldown)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = attackRange;
            definition.AttacksPerSecond = 1f / attackCooldown;
            return definition;
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class StatusResolverTests
    {
        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void ApplyStun_CancelsWindupAndExpiresAtDeadline()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, 0.25f);
            var events = new BattleEventQueue();
            loop.Tick(simulation, events);
            Assert.AreEqual(UnitAttackPhase.Windup, attacker.AttackPhase);

            StatusDefinition stun = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            stun.Kind = StatusKind.Stun;
            stun.Category = StatusCategory.HarmfulCrowdControl;
            stun.StackingRule = StatusStackingRule.RefreshPerSource;
            stun.DefaultDuration = 0.5f;

            StatusApplicationResult result = StatusResolver.TryApply(
                simulation,
                attacker,
                new StatusApplicationRequest(stun, 2),
                events);

            Assert.AreEqual(StatusApplicationResult.Applied, result);
            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            Assert.IsTrue(attacker.StatusSnapshot.BlocksAttack);
            AssertEvent(events, BattleEventType.AttackWindupCancelled);
            AssertEvent(events, BattleEventType.StatusApplied);

            loop.Tick(simulation, events);
            Assert.AreEqual(1, attacker.Statuses.Count);
            loop.Tick(simulation, events);
            Assert.AreEqual(0, attacker.Statuses.Count);
            Assert.IsFalse(attacker.StatusSnapshot.BlocksAttack);
            AssertEvent(events, BattleEventType.StatusRemoved);
        }

        [Test]
        public void ApplySameSource_RefreshesWithoutAllocatingAnotherSlot()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[0];
            StatusDefinition slow = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            slow.Kind = StatusKind.Slow;
            slow.Category = StatusCategory.HarmfulCrowdControl;
            slow.StackingRule = StatusStackingRule.RefreshPerSource;
            slow.DefaultDuration = 1f;
            slow.DefaultMagnitude = 0.2f;

            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(slow, 2)));
            Assert.AreEqual(StatusApplicationResult.Refreshed, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(slow, 2, magnitude: 0.4f)));

            Assert.AreEqual(1, target.Statuses.Count);
            Assert.That(target.StatusSnapshot.Slow, Is.EqualTo(0.4f).Within(0.0001f));
        }

        private static BattleSimulation CreateSimulation()
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("attacker", 1);
            UnitDefinition target = TestDefinitions.CreateUnit("target", 1);
            target.AttackCooldown = 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                });
        }

        private static void AssertEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type) return;
            }
            Assert.Fail("Expected event " + type + ".");
        }
    }
}

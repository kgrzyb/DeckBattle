using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class ProjectileResolverTests
    {
        [Test]
        public void ResolveCombat_RangedWithProjectileLaunchesWithoutImmediateDamage()
        {
            UnitDefinition attacker = CreateUnit("archer", 10, 3, 3, 1f, UnitType.Range);
            attacker.Projectile = CreateProjectile("arrow", 1f);
            BattleSimulation simulation = CreateSimulation(attacker, new HexCoord(1, 1), CreateUnit("target", 10, 1, 1, 1f, UnitType.Melee), new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 0.25f, events);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(0, result.TotalDamage);
            Assert.AreEqual(10, simulation.Units[1].CurrentHp);
            Assert.AreEqual(1f, simulation.Units[0].AttackCooldownRemaining);
            Assert.AreEqual(10, simulation.Units[0].CurrentMana);
            Assert.AreEqual(0, simulation.Units[1].CurrentMana);
            Assert.AreEqual(1, simulation.Projectiles.Count);
            AssertEventTypeExists(events, BattleEventType.ProjectileLaunched);
            AssertEventTypeDoesNotExist(events, BattleEventType.UnitDamaged);
        }

        [Test]
        public void ResolveProjectiles_AppliesDamageAfterTravelTime()
        {
            BattleSimulation simulation = CreateProjectileSimulation(1f);
            var events = new BattleEventQueue();

            ProjectileResolutionResult early = ProjectileResolver.ResolveProjectiles(simulation, 0.5f, events);

            Assert.AreEqual(0, early.Hits);
            Assert.AreEqual(10, simulation.Units[1].CurrentHp);
            Assert.AreEqual(1, simulation.Projectiles.Count);

            events.Clear();
            ProjectileResolutionResult hit = ProjectileResolver.ResolveProjectiles(simulation, 0.5f, events);

            Assert.AreEqual(1, hit.Hits);
            Assert.AreEqual(3, hit.TotalDamage);
            Assert.AreEqual(7, simulation.Units[1].CurrentHp);
            Assert.AreEqual(10, simulation.Units[1].CurrentMana);
            Assert.AreEqual(0, simulation.Projectiles.Count);
            AssertEventTypeExists(events, BattleEventType.ProjectileHit);
            AssertEventTypeExists(events, BattleEventType.UnitDamaged);
        }

        [Test]
        public void ResolveProjectiles_HitsLiveTargetThatMovedOutOfOriginalRange()
        {
            BattleSimulation simulation = CreateProjectileSimulation(1f);

            simulation.MoveUnit(simulation.Units[1], new HexCoord(4, 4));
            ProjectileResolver.ResolveProjectiles(simulation, 1f, null);

            Assert.AreEqual(7, simulation.Units[1].CurrentHp);
            Assert.AreEqual(0, simulation.Projectiles.Count);
        }

        [Test]
        public void ResolveProjectiles_DoesNotDamageTargetDefeatedBeforeImpact()
        {
            BattleSimulation simulation = CreateProjectileSimulation(1f);

            simulation.DefeatUnit(simulation.Units[1]);
            ProjectileResolver.ResolveProjectiles(simulation, 1f, null);

            Assert.IsTrue(simulation.Units[1].IsDefeated);
            Assert.AreEqual(0, simulation.Units[1].CurrentHp);
            Assert.AreEqual(0, simulation.Projectiles.Count);
        }

        [Test]
        public void ResolveProjectiles_EmitsCritAndDamageManaAtImpact()
        {
            UnitDefinition attacker = CreateUnit("archer", 10, 3, 3, 1f, UnitType.Range);
            attacker.Projectile = CreateProjectile("arrow", 1f);
            attacker.CritChance = 100f;
            UnitDefinition target = CreateUnit("target", 10, 1, 1, 1f, UnitType.Melee);
            target.ManaPerDamageTaken = 7;
            BattleSimulation simulation = CreateSimulation(attacker, new HexCoord(1, 1), target, new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            CombatResolver.ResolveCombat(simulation, 0.1f, events);

            AssertEventTypeDoesNotExist(events, BattleEventType.UnitCrit);
            Assert.AreEqual(0, simulation.Units[1].CurrentMana);

            events.Clear();
            ProjectileResolver.ResolveProjectiles(simulation, 1f, events);

            AssertEventTypeExists(events, BattleEventType.UnitCrit);
            AssertManaChangedEventExists(events, 2, 7);
        }

        [Test]
        public void Tick_DoesNotEndBattleBeforeLethalProjectileLands()
        {
            UnitDefinition attacker = CreateUnit("archer", 10, 10, 3, 1f, UnitType.Range);
            attacker.Projectile = CreateProjectile("arrow", 1f);
            UnitDefinition target = CreateUnit("target", 3, 0, 1, 1f, UnitType.Melee);
            BattleSimulation simulation = CreateSimulation(attacker, new HexCoord(1, 1), target, new HexCoord(2, 1));
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult launchTick = loop.Tick(simulation, events);

            Assert.IsFalse(launchTick.BattleEnded);
            Assert.IsTrue(simulation.Units[1].IsAlive);
            Assert.AreEqual(1, simulation.Projectiles.Count);

            BattleTickResult hitTick = loop.Tick(simulation, events);

            Assert.IsTrue(hitTick.BattleEnded);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
        }

        [Test]
        public void Tick_DoesNotEndBattleWhileProjectileFromDefeatedUnitIsActive()
        {
            UnitDefinition attacker = CreateUnit("archer", 3, 10, 3, 1f, UnitType.Range);
            attacker.Projectile = CreateProjectile("arrow", 1f);
            UnitDefinition target = CreateUnit("target", 10, 3, 3, 1f, UnitType.Range);
            BattleSimulation simulation = CreateSimulation(attacker, new HexCoord(1, 1), target, new HexCoord(2, 1));
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult launchTick = loop.Tick(simulation, events);

            Assert.IsFalse(launchTick.BattleEnded);
            Assert.IsTrue(simulation.Units[0].IsDefeated);
            Assert.AreEqual(1, simulation.Projectiles.Count);

            BattleTickResult hitTick = loop.Tick(simulation, events);

            Assert.IsTrue(hitTick.BattleEnded);
            Assert.IsFalse(hitTick.HasWinner);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
        }

        [Test]
        public void ResolveCombat_RangedWithoutProjectileKeepsImmediateDamageFallback()
        {
            BattleSimulation simulation = CreateSimulation(
                CreateUnit("ranged", 10, 3, 3, 1f, UnitType.Range),
                new HexCoord(1, 1),
                CreateUnit("target", 10, 1, 1, 1f, UnitType.Melee),
                new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);

            CombatResolutionResult result = CombatResolver.ResolveCombat(simulation, 0.25f);

            Assert.AreEqual(1, result.Attacks);
            Assert.AreEqual(3, result.TotalDamage);
            Assert.AreEqual(7, simulation.Units[1].CurrentHp);
            Assert.AreEqual(0, simulation.Projectiles.Count);
        }

        private static BattleSimulation CreateProjectileSimulation(float projectileSpeed)
        {
            UnitDefinition attacker = CreateUnit("archer", 10, 3, 3, 1f, UnitType.Range);
            attacker.Projectile = CreateProjectile("arrow", projectileSpeed);
            BattleSimulation simulation = CreateSimulation(attacker, new HexCoord(1, 1), CreateUnit("target", 10, 1, 1, 1f, UnitType.Melee), new HexCoord(2, 1));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            CombatResolver.ResolveCombat(simulation, 0.1f);
            return simulation;
        }

        private static BattleSimulation CreateSimulation(
            UnitDefinition attacker,
            HexCoord attackerHex,
            UnitDefinition target,
            HexCoord targetHex)
        {
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, attackerHex),
                    new UnitSpawnData(2, target, BattleSide.Enemy, targetHex)
                });
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, float attackCooldown, UnitType unitType)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1, unitType);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = attackRange;
            definition.AttackCooldown = attackCooldown;
            return definition;
        }

        private static ProjectileDefinition CreateProjectile(string projectileId, float speed)
        {
            ProjectileDefinition definition = ScriptableObject.CreateInstance<ProjectileDefinition>();
            definition.ProjectileId = projectileId;
            definition.Speed = speed;
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

        private static void AssertEventTypeDoesNotExist(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    Assert.Fail("Event type should not have been emitted: " + type);
                }
            }
        }

        private static void AssertManaChangedEventExists(BattleEventQueue events, int unitId, int currentMana)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.UnitManaChanged
                    && battleEvent.UnitId == unitId
                    && battleEvent.CurrentMana == currentMana)
                {
                    return;
                }
            }

            Assert.Fail("Expected mana event was not emitted for unit " + unitId + " with mana " + currentMana + ".");
        }
    }
}

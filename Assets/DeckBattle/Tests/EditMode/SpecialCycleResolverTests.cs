using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class SpecialCycleResolverTests
    {
        private const float TickDuration = 0.25f;

        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Tick_ReadySpecialStartsWindupWithoutApplyingEffectOrSpendingMana()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Windup, unit.SpecialPhase);
            Assert.That(unit.SpecialWindupEndTime, Is.EqualTo(0.75d).Within(0.000001d));
            Assert.AreEqual(unit.CombatSpec.ManaThreshold, unit.CurrentMana);
            Assert.IsFalse(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialWindupStarted);
        }

        [Test]
        public void Tick_SpecialWindupIgnoresHasteAndCompletesAtConfiguredDeadline()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(CreateHasteStatus(5f, 0.5f), unit.UnitId));
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            AssertEvent(events, BattleEventType.UnitSpecialActivated);
        }

        [Test]
        public void Tick_SpecialCastSpendsManaAtCastStartAndLocksManaUntilRecoveryEnds()
        {
            BattleSimulation simulation = CreateSimulation(0.25f, 0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Casting, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            Assert.That(unit.NextAttackTime, Is.EqualTo(10.5d).Within(0.000001d));
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(0, unit.CurrentMana);

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.That(unit.NextAttackTime, Is.EqualTo(5.5d).Within(0.000001d));
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(0, unit.CurrentMana);

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(10, unit.CurrentMana);
        }

        [Test]
        public void Tick_SpecialAttackCooldownWaitsForCastToEndWhenItCompletesEarly()
        {
            BattleSimulation simulation = CreateSimulation(0.25f, 1f, attacksPerSecond: 4f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            unit.SetTarget(simulation.Units[1]);
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Casting, unit.SpecialPhase);
            Assert.That(unit.NextAttackTime, Is.EqualTo(0.75d).Within(0.000001d));

            loop.Tick(events);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);
            loop.Tick(events);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);
            loop.Tick(events);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);

            loop.Tick(events);
            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.AreEqual(UnitAttackPhase.Windup, unit.AttackPhase);
            AssertEvent(events, BattleEventType.AttackWindupStarted);
        }

        [Test]
        public void Tick_RecoveryLockUsesRuntimeTuningDuration()
        {
            BattleRuntimeTuning tuning = new BattleRuntimeTuning(
                1f,
                0,
                specialRecoveryLockDuration: 0.75f);
            BattleSimulation simulation = CreateSimulation(0.25f, tuning: tuning);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.That(unit.ManaLockEndTime, Is.EqualTo(1.25d).Within(0.000001d));

            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());
            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);

            loop.Tick(new BattleEventQueue());
            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);
        }

        [Test]
        public void StatusStun_CancelsCastAfterManaWasSpent()
        {
            BattleSimulation simulation = CreateSimulation(0.25f, 1f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());
            var events = new BattleEventQueue();

            StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(CreateStunStatus(), 0),
                events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            Assert.IsFalse(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialWindupCancelled);
        }

        [Test]
        public void Tick_StartedAttackFiresBeforeItsReadySpecialBegins()
        {
            BattleSimulation simulation = CreateSimulation(0.25f);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.NextAttackTime = 0d;
            attacker.SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            TestDefinitions.ResolveNextAttack(simulation, events, TickDuration);

            AssertEvent(events, BattleEventType.AttackFired);
            Assert.AreEqual(attacker.CombatSpec.ManaThreshold, attacker.CurrentMana);
            Assert.AreEqual(UnitSpecialPhase.Windup, attacker.SpecialPhase);
            AssertEvent(events, BattleEventType.SpecialWindupStarted);
        }

        [Test]
        public void StatusStun_CancelsSpecialWindupWithoutSpendingMana()
        {
            BattleSimulation simulation = CreateSimulation(1f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            loop.Tick(new BattleEventQueue());
            var events = new BattleEventQueue();

            StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(CreateStunStatus(), 0),
                events);

            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);
            Assert.AreEqual(unit.CombatSpec.ManaThreshold, unit.CurrentMana);
            AssertEvent(events, BattleEventType.SpecialWindupCancelled);
        }

        [Test]
        public void Tick_ReadySpecialBlocksNewAttackWindup()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.NextAttackTime = 0d;
            attacker.SetTarget(simulation.Units[1]);
            var events = new BattleEventQueue();

            new BattleTickLoop(simulation, TickDuration).Tick(events);

            Assert.AreEqual(UnitAttackPhase.AcquireReload, attacker.AttackPhase);
            Assert.AreEqual(UnitSpecialPhase.Windup, attacker.SpecialPhase);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);
            AssertEvent(events, BattleEventType.SpecialWindupStarted);
        }

        [Test]
        public void Tick_ReadySpecialWaitsForActiveMovementThenStartsBeforeAnotherStep()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            simulation.StartUnitMovement(unit, new HexCoord(0, 1));
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());

            Assert.IsTrue(unit.IsMoving);
            Assert.AreEqual(UnitSpecialPhase.Idle, unit.SpecialPhase);

            loop.Tick(new BattleEventQueue());

            Assert.IsFalse(unit.IsMoving);
            Assert.AreEqual(UnitSpecialPhase.Windup, unit.SpecialPhase);
        }

        [Test]
        public void FurySwipes_DealsTenSeventyPercentHitsAcrossConfiguredCastDuration()
        {
            BattleSimulation simulation = CreateFurySimulation(2000);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(target);
            var loop = new BattleTickLoop(simulation, 0.15f);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(0, CountEvents(events, BattleEventType.UnitDamaged));
            AssertEvent(events, BattleEventType.SpecialCastStarted);

            int totalDamage = 0;
            int strikeCount = 0;
            for (int i = 0; i < 10; i++)
            {
                loop.Tick(events);
                strikeCount += CountEvents(events, BattleEventType.SpecialStrikeFired);
                totalDamage += SumDamageEvents(events);
            }

            Assert.AreEqual(10, strikeCount);
            Assert.AreEqual(700, totalDamage);
            Assert.AreEqual(1300, target.CurrentHp);
            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, attacker.SpecialPhase);
            AssertEvent(events, BattleEventType.UnitSpecialActivated);
        }

        [Test]
        public void FurySwipes_ChargedOutsideRangeAllowsMovementButCannotStartWindup()
        {
            BattleSimulation simulation = CreateFurySimulation(2000, new HexCoord(4, 1));
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);

            Assert.IsFalse(UnitActionRules.CanStartSpecialWindup(simulation, attacker));
            Assert.IsTrue(UnitActionRules.CanStartMovement(simulation, attacker));
        }

        [Test]
        public void FurySwipes_TargetDeathDuringCastEndsRemainingStrikesWithoutManaRefund()
        {
            BattleSimulation simulation = CreateFurySimulation(70);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);
            var loop = new BattleTickLoop(simulation, 0.15f);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);
            loop.Tick(events);
            loop.Tick(events);

            Assert.IsFalse(simulation.Units[1].IsAlive);
            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, attacker.SpecialPhase);
            Assert.AreEqual(1, CountEvents(events, BattleEventType.SpecialStrikeFired));
            Assert.AreEqual(70, SumDamageEvents(events));
        }

        [Test]
        public void MegaArrow_LaunchesProjectileThenDealsOneHundredFiftyPercentDamageAndStunsAtImpact()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(target);
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(1, simulation.Projectiles.Count);
            Assert.AreEqual(500, target.CurrentHp);
            Assert.IsFalse(target.Statuses.TryFind(StatusKind.Stun, attacker.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialCastStarted);
            AssertEvent(events, BattleEventType.SpecialStrikeFired);
            AssertEvent(events, BattleEventType.ProjectileLaunched);

            events.Clear();
            ProjectileResolutionResult result = ProjectileResolver.ResolveProjectiles(simulation, 1f, events);

            Assert.AreEqual(1, result.Hits);
            Assert.AreEqual(150, result.TotalDamage);
            Assert.AreEqual(350, target.CurrentHp);
            Assert.IsTrue(target.Statuses.TryFind(StatusKind.Stun, attacker.UnitId, out int stunIndex));
            Assert.That(target.Statuses[stunIndex].EndTime, Is.EqualTo(simulation.ElapsedTime + 1d).Within(0.000001d));
            AssertEvent(events, BattleEventType.StatusApplied);
        }

        [Test]
        public void MegaArrow_CastDurationMeasuresTheWholeSpecialFromWindupStart()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());

            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);

            var events = new BattleEventQueue();
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, attacker.SpecialPhase);
            AssertEvent(events, BattleEventType.UnitSpecialActivated);
        }

        [Test]
        public void MegaArrow_OutsideRangeDoesNotStartWindupAndAllowsMovement()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500, new HexCoord(4, 1));
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);

            Assert.IsFalse(UnitActionRules.CanStartSpecialWindup(simulation, attacker));
            Assert.IsTrue(UnitActionRules.CanStartMovement(simulation, attacker));
        }

        [Test]
        public void MegaArrow_TargetDeathDuringWindupCancelsWithoutManaSpend()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500);
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            simulation.DefeatUnit(simulation.Units[1]);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Idle, attacker.SpecialPhase);
            Assert.AreEqual(attacker.CombatSpec.ManaThreshold, attacker.CurrentMana);
            Assert.AreEqual(0, simulation.Projectiles.Count);
            AssertEvent(events, BattleEventType.SpecialWindupCancelled);
        }

        [Test]
        public void Slam_AtImpactDamagesAllAndOnlyEnemiesWithinRadius()
        {
            BattleSimulation simulation = CreateSlamSimulation();
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState nearbyEnemyA = simulation.Units[1];
            UnitRuntimeState nearbyEnemyB = simulation.Units[2];
            UnitRuntimeState distantEnemy = simulation.Units[3];
            UnitRuntimeState friendly = simulation.Units[4];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Windup, attacker.SpecialPhase);
            Assert.AreEqual(10, attacker.CurrentMana);
            Assert.AreEqual(0, CountEvents(events, BattleEventType.UnitDamaged));

            loop.Tick(events);

            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(1, CountEvents(events, BattleEventType.SpecialAreaImpact));
            Assert.AreEqual(2, CountEvents(events, BattleEventType.UnitDamaged));
            Assert.AreEqual(900, nearbyEnemyA.CurrentHp);
            Assert.AreEqual(900, nearbyEnemyB.CurrentHp);
            Assert.AreEqual(1000, distantEnemy.CurrentHp);
            Assert.AreEqual(1000, friendly.CurrentHp);
        }

        [Test]
        public void Slam_SimultaneousOpposingImpactsBothResolve()
        {
            UnitDefinition player = CreateSlamUnit("player-slam");
            UnitDefinition enemy = CreateSlamUnit("enemy-slam");
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 1))
                });
            simulation.Units[0].CurrentMana = 10;
            simulation.Units[1].CurrentMana = 10;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(2, CountEvents(events, BattleEventType.SpecialAreaImpact));
            Assert.AreEqual(900, simulation.Units[0].CurrentHp);
            Assert.AreEqual(900, simulation.Units[1].CurrentHp);
        }

        private static BattleSimulation CreateSimulation(
            float windupDuration,
            float castDuration = 0f,
            BattleRuntimeTuning? tuning = null,
            float attacksPerSecond = 0.1f)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("attacker", 1);
            attacker.AttacksPerSecond = attacksPerSecond;
            attacker.ManaThreshold = 10;
            attacker.Special = CreateHasteBurstSpecial(windupDuration, castDuration);
            UnitDefinition target = TestDefinitions.CreateUnit("target", 1);
            target.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                },
                tuning ?? BattleRuntimeTuning.Default);
        }

        private static BattleSimulation CreateFurySimulation(int targetHp, HexCoord? targetHex = null)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("fury-attacker", 100);
            attacker.Attack = 100;
            attacker.AttacksPerSecond = 0.1f;
            attacker.ManaThreshold = 10;
            attacker.ManaPerTick = 0;
            attacker.Special = CreateFurySwipesSpecial();
            UnitDefinition target = TestDefinitions.CreateUnit("fury-target", 1);
            target.MaxHp = targetHp;
            target.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, targetHex ?? new HexCoord(2, 1))
                });
        }

        private static BattleSimulation CreateMegaArrowSimulation(int targetHp, HexCoord? targetHex = null)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("mega-arrow-attacker", 1, UnitType.Range);
            attacker.MaxHp = 1000;
            attacker.Attack = 100;
            attacker.AttackRange = 2;
            attacker.AttacksPerSecond = 0.001f;
            attacker.ManaThreshold = 10;
            attacker.ManaPerTick = 0;
            attacker.Special = CreateMegaArrowSpecial();
            UnitDefinition target = TestDefinitions.CreateUnit("mega-arrow-target", 1);
            target.MaxHp = targetHp;
            target.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, targetHex ?? new HexCoord(2, 1))
                });
        }

        private static BattleSimulation CreateSlamSimulation()
        {
            UnitDefinition attacker = CreateSlamUnit("slam-attacker");
            UnitDefinition nearbyEnemyA = CreatePassiveUnit("nearby-enemy-a");
            UnitDefinition nearbyEnemyB = CreatePassiveUnit("nearby-enemy-b");
            UnitDefinition distantEnemy = CreatePassiveUnit("distant-enemy");
            UnitDefinition friendly = CreatePassiveUnit("friendly");
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(2, 2)),
                    new UnitSpawnData(2, nearbyEnemyA, BattleSide.Enemy, new HexCoord(3, 2)),
                    new UnitSpawnData(3, nearbyEnemyB, BattleSide.Enemy, new HexCoord(2, 3)),
                    new UnitSpawnData(4, distantEnemy, BattleSide.Enemy, new HexCoord(4, 2)),
                    new UnitSpawnData(5, friendly, BattleSide.Player, new HexCoord(1, 2))
                });
        }

        private static UnitDefinition CreateSlamUnit(string unitId)
        {
            UnitDefinition unit = TestDefinitions.CreateUnit(unitId, 1);
            unit.MaxHp = 1000;
            unit.Attack = 100;
            unit.AttacksPerSecond = 0.001f;
            unit.ManaThreshold = 10;
            unit.ManaPerTick = 0;
            unit.Special = CreateSlamSpecial();
            return unit;
        }

        private static UnitDefinition CreatePassiveUnit(string unitId)
        {
            UnitDefinition unit = TestDefinitions.CreateUnit(unitId, 1);
            unit.MaxHp = 1000;
            unit.AttacksPerSecond = 0.001f;
            unit.ManaPerTick = 0;
            return unit;
        }

        private static UnitSpecialDefinition CreateHasteBurstSpecial(float windupDuration, float castDuration = 0f)
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.HasteBurst;
            special.WindupDuration = windupDuration;
            special.CastDuration = castDuration;
            special.AppliedStatus = CreateHasteStatus(5f, 0.5f);
            return special;
        }

        private static UnitSpecialDefinition CreateFurySwipesSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.FurySwipes;
            special.WindupDuration = 0.2f;
            special.CastDuration = 1.5f;
            special.StrikeCount = 10;
            special.AttackDamageMultiplier = 0.7f;
            return special;
        }

        private static UnitSpecialDefinition CreateSlamSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.Slam;
            special.WindupDuration = TickDuration;
            special.CastDuration = TickDuration;
            special.AttackDamageMultiplier = 1f;
            special.EffectRadius = 1;
            return special;
        }

        private static UnitSpecialDefinition CreateMegaArrowSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.MegaArrow;
            special.WindupDuration = TickDuration;
            special.CastDuration = 0.5f;
            special.AttackDamageMultiplier = 1.5f;
            special.AppliedStatus = CreateStunStatus(2f);
            special.AppliedStatusLifetimeMode = StatusLifetimeMode.OverrideSeconds;
            special.AppliedStatusDurationOverride = 1f;
            special.Projectile = CreateProjectile("mega-arrow", 1f);
            return special;
        }

        private static StatusDefinition CreateHasteStatus(float duration, float magnitude)
        {
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;
            status.Category = StatusCategory.Beneficial;
            status.DefaultDuration = duration;
            status.DefaultMagnitude = magnitude;
            return status;
        }

        private static ProjectileDefinition CreateProjectile(string projectileId, float speed)
        {
            ProjectileDefinition projectile = TestDefinitions.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
            projectile.ProjectileId = projectileId;
            projectile.Speed = speed;
            return projectile;
        }

        private static StatusDefinition CreateStunStatus(float duration = 1f)
        {
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Stun;
            status.Category = StatusCategory.HarmfulCrowdControl;
            status.DefaultDuration = duration;
            return status;
        }

        private static void AssertEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type) return;
            }

            Assert.Fail("Expected event: " + type);
        }

        private static void AssertNoEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreNotEqual(type, events[i].Type);
            }
        }

        private static int CountEvents(BattleEventQueue events, BattleEventType type)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static int SumDamageEvents(BattleEventQueue events)
        {
            int total = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == BattleEventType.UnitDamaged)
                {
                    total += events[i].Amount;
                }
            }

            return total;
        }
    }
}

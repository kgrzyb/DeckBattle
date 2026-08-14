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
        public void Tick_ReadySpecialStartsCastAndSpendsManaBeforeEffect()
        {
            BattleSimulation simulation = CreateSimulation(0.5f);
            UnitRuntimeState unit = simulation.Units[0];
            unit.CurrentMana = unit.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.Casting, unit.SpecialPhase);
            Assert.That(unit.SpecialEffectTime, Is.EqualTo(0.75d).Within(0.000001d));
            Assert.AreEqual(0, unit.CurrentMana);
            Assert.IsFalse(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialCastStarted);
        }

        [Test]
        public void Tick_SpecialCastIgnoresHasteAndCompletesAtConfiguredDeadline()
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
            AssertEvent(events, BattleEventType.SpecialCastCompleted);
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
            Assert.That(unit.NextAttackTime, Is.EqualTo(5.25d).Within(0.000001d));
            CombatResolver.AddMana(simulation, unit, 10, events);
            Assert.AreEqual(0, unit.CurrentMana);

            loop.Tick(events);
            loop.Tick(events);

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.That(unit.NextAttackTime, Is.EqualTo(5.25d).Within(0.000001d));
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
            Assert.That(unit.NextAttackTime, Is.EqualTo(0.375d).Within(0.000001d));

            loop.Tick(events);
            loop.Tick(events);
            loop.Tick(events);

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
            Assert.IsTrue(unit.Statuses.TryFind(StatusKind.Haste, unit.UnitId, out _));
            AssertEvent(events, BattleEventType.SpecialCastCancelled);
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
            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            AssertNoEvent(events, BattleEventType.SpecialCastCancelled);
        }

        [Test]
        public void StatusStun_CancelsCastBeforeEffectWithoutManaRefund()
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

            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, unit.SpecialPhase);
            Assert.AreEqual(0, unit.CurrentMana);
            AssertEvent(events, BattleEventType.SpecialCastCancelled);
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
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            AssertNoEvent(events, BattleEventType.AttackWindupStarted);
            AssertEvent(events, BattleEventType.SpecialCastStarted);
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
            Assert.AreEqual(UnitSpecialPhase.Casting, unit.SpecialPhase);
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

            int totalDamage = 0;
            int strikeCount = 0;
            bool castCancelled = false;
            bool castCompleted = false;
            for (int i = 0; i < 3; i++)
            {
                loop.Tick(events);
                strikeCount += CountEvents(events, BattleEventType.SpecialStrikeFired);
                totalDamage += SumDamageEvents(events);
                castCancelled |= CountEvents(events, BattleEventType.SpecialCastCancelled) > 0;
                castCompleted |= CountEvents(events, BattleEventType.SpecialCastCompleted) > 0;
            }

            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(1, CountEvents(events, BattleEventType.UnitDamaged));
            Assert.IsFalse(castCancelled);

            for (int i = 0; i < 11; i++)
            {
                loop.Tick(events);
                strikeCount += CountEvents(events, BattleEventType.SpecialStrikeFired);
                totalDamage += SumDamageEvents(events);
                castCancelled |= CountEvents(events, BattleEventType.SpecialCastCancelled) > 0;
                castCompleted |= CountEvents(events, BattleEventType.SpecialCastCompleted) > 0;
            }

            Assert.AreEqual(10, strikeCount);
            Assert.AreEqual(700, totalDamage);
            Assert.AreEqual(1300, target.CurrentHp);
            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, attacker.SpecialPhase);
            Assert.IsFalse(castCancelled);
            Assert.IsTrue(castCompleted);
        }

        [Test]
        public void FurySwipes_ChargedOutsideRangeAllowsMovementButCannotStartCast()
        {
            BattleSimulation simulation = CreateFurySimulation(2000, new HexCoord(4, 1));
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);

            Assert.IsFalse(UnitActionRules.CanStartSpecialCast(simulation, attacker));
            Assert.IsTrue(UnitActionRules.CanStartMovement(simulation, attacker));
        }

        [Test]
        public void FurySwipes_TargetDeathBeforeFirstStrikeKeepsDeadTargetWhenNoReplacementExists()
        {
            BattleSimulation simulation = CreateFurySimulation(2000);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(target);
            var loop = new BattleTickLoop(simulation, 0.15f);

            loop.Tick(new BattleEventQueue());
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(target.UnitId, attacker.LockedSpecialTargetUnitId);
            simulation.DefeatUnit(target);
            var events = new BattleEventQueue();

            loop.Tick(events);
            BattleTickResult castTick = loop.Tick(events);

            Assert.IsFalse(castTick.BattleEnded);
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(target.UnitId, attacker.LockedSpecialTargetUnitId);
            AssertNoEvent(events, BattleEventType.SpecialCastCancelled);
        }

        [Test]
        public void FurySwipes_TargetDeathBeforeFirstStrikeRetargetsUsingSpecialTargetRules()
        {
            BattleSimulation simulation = CreateFurySimulation(2000, includeReplacementTarget: true);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState defeatedTarget = simulation.Units[1];
            UnitRuntimeState replacementTarget = simulation.Units[2];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(defeatedTarget);
            var loop = new BattleTickLoop(simulation, 0.15f);

            loop.Tick(new BattleEventQueue());
            simulation.DefeatUnit(defeatedTarget);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(replacementTarget.UnitId, attacker.LockedSpecialTargetUnitId);
            Assert.AreEqual(replacementTarget.UnitId, attacker.TargetUnitId);
            Assert.AreEqual(1, CountEvents(events, BattleEventType.SpecialStrikeFired));
            Assert.AreEqual(replacementTarget.UnitId, FindEvent(events, BattleEventType.SpecialStrikeFired).TargetUnitId);
            Assert.AreEqual(replacementTarget.UnitId, FindEvent(events, BattleEventType.UnitTargetChanged).TargetUnitId);
        }

        [Test]
        public void FurySwipes_TargetDeathAfterFirstStrikeDoesNotRetarget()
        {
            BattleSimulation simulation = CreateFurySimulation(2000, includeReplacementTarget: true);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState firstTarget = simulation.Units[1];
            UnitRuntimeState replacementTarget = simulation.Units[2];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(firstTarget);
            var loop = new BattleTickLoop(simulation, 0.15f);

            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());
            simulation.DefeatUnit(firstTarget);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(firstTarget.UnitId, attacker.LockedSpecialTargetUnitId);
            Assert.AreNotEqual(replacementTarget.UnitId, FindEvent(events, BattleEventType.SpecialStrikeFired).TargetUnitId);
        }

        [Test]
        public void FurySwipes_TargetDeathDuringCastCompletesRemainingStrikesWithoutManaRefund()
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
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(1, CountEvents(events, BattleEventType.SpecialStrikeFired));
            Assert.AreEqual(0, SumDamageEvents(events));

            int strikesAfterDeath = 0;
            for (int i = 0; i < 9; i++)
            {
                loop.Tick(events);
                strikesAfterDeath += CountEvents(events, BattleEventType.SpecialStrikeFired);
            }

            Assert.AreEqual(7, strikesAfterDeath);
            Assert.AreEqual(UnitSpecialPhase.RecoveryLock, attacker.SpecialPhase);
            Assert.AreEqual(0, SumDamageEvents(events));
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
            AssertNoEvent(events, BattleEventType.SpecialCastCancelled);
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
        public void MegaArrow_CastDurationMeasuresTheWholeSpecialFromCastStart()
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
            AssertEvent(events, BattleEventType.SpecialCastCompleted);
        }

        [Test]
        public void MegaArrow_OutsideRangeDoesNotStartCastAndAllowsMovement()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500, new HexCoord(4, 1));
            UnitRuntimeState attacker = simulation.Units[0];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(simulation.Units[1]);

            Assert.IsFalse(UnitActionRules.CanStartSpecialCast(simulation, attacker));
            Assert.IsTrue(UnitActionRules.CanStartMovement(simulation, attacker));
        }

        [Test]
        public void MegaArrow_TargetDeathAfterCastStartLaunchesProjectileMissAndSpendsMana()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(target);
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            simulation.DefeatUnit(target);
            var events = new BattleEventQueue();

            BattleTickResult result = loop.Tick(events);

            Assert.IsFalse(result.BattleEnded);
            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(0, attacker.CurrentMana);
            Assert.AreEqual(1, simulation.Projectiles.Count);
            Assert.AreEqual(target.UnitId, simulation.Projectiles[0].TargetUnitId);
            AssertEvent(events, BattleEventType.ProjectileLaunched);
            AssertNoEvent(events, BattleEventType.SpecialCastCancelled);
        }

        [Test]
        public void MegaArrow_TargetDeathBeforePayloadRetargetsUsingSpecialTargetRules()
        {
            BattleSimulation simulation = CreateMegaArrowSimulation(500, includeReplacementTarget: true);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState defeatedTarget = simulation.Units[1];
            UnitRuntimeState replacementTarget = simulation.Units[2];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            attacker.SetTarget(defeatedTarget);
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            simulation.DefeatUnit(defeatedTarget);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(replacementTarget.UnitId, attacker.LockedSpecialTargetUnitId);
            Assert.AreEqual(replacementTarget.UnitId, attacker.TargetUnitId);
            Assert.AreEqual(1, simulation.Projectiles.Count);
            Assert.AreEqual(replacementTarget.UnitId, simulation.Projectiles[0].TargetUnitId);
            Assert.AreEqual(replacementTarget.UnitId, FindEvent(events, BattleEventType.UnitTargetChanged).TargetUnitId);
        }

        [Test]
        public void Longshot_LocksLowestHpTargetAtCastStartAndDoesNotRetargetBeforeFiring()
        {
            BattleSimulation simulation = CreateLongshotSimulation(twoEnemies: true);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState firstEnemy = simulation.Units[1];
            UnitRuntimeState lockedEnemy = simulation.Units[2];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            firstEnemy.CurrentHp = 400;
            lockedEnemy.CurrentHp = 300;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(lockedEnemy.UnitId, attacker.LockedSpecialTargetUnitId);
            firstEnemy.CurrentHp = 1;
            events.Clear();

            loop.Tick(events);

            Assert.AreEqual(1, simulation.Projectiles.Count);
            Assert.AreEqual(lockedEnemy.UnitId, simulation.Projectiles[0].TargetUnitId);
            AssertEvent(events, BattleEventType.ProjectileLaunched);
        }

        [Test]
        public void Longshot_FiresAtDeadLockedTargetAndProjectileResolvesAsMiss()
        {
            BattleSimulation simulation = CreateLongshotSimulation(twoEnemies: false);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);
            var events = new BattleEventQueue();

            loop.Tick(events);
            Assert.AreEqual(target.UnitId, attacker.LockedSpecialTargetUnitId);
            simulation.DefeatUnit(target);
            events.Clear();

            BattleTickResult launchTick = loop.Tick(events);

            Assert.IsFalse(launchTick.BattleEnded);
            Assert.AreEqual(1, simulation.Projectiles.Count);
            Assert.AreEqual(target.UnitId, simulation.Projectiles[0].TargetUnitId);
            AssertEvent(events, BattleEventType.ProjectileLaunched);

            events.Clear();
            ProjectileResolutionResult result = ProjectileResolver.ResolveProjectiles(simulation, 5f, events);

            Assert.AreEqual(0, result.Hits);
            Assert.AreEqual(0, result.TotalDamage);
            Assert.AreEqual(0, simulation.Projectiles.Count);
            Assert.AreEqual(0, FindEvent(events, BattleEventType.ProjectileResolved).Amount);
            AssertNoEvent(events, BattleEventType.ProjectileHit);
            AssertNoEvent(events, BattleEventType.UnitDamaged);
        }

        [Test]
        public void Longshot_TargetDeathBeforePayloadRetargetsUsingLowestHpSpecialRule()
        {
            BattleSimulation simulation = CreateLongshotSimulation(twoEnemies: true);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState replacementTarget = simulation.Units[1];
            UnitRuntimeState defeatedTarget = simulation.Units[2];
            replacementTarget.CurrentHp = 400;
            defeatedTarget.CurrentHp = 300;
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            Assert.AreEqual(defeatedTarget.UnitId, attacker.LockedSpecialTargetUnitId);
            simulation.DefeatUnit(defeatedTarget);
            var events = new BattleEventQueue();

            loop.Tick(events);

            Assert.AreEqual(replacementTarget.UnitId, attacker.LockedSpecialTargetUnitId);
            Assert.AreEqual(replacementTarget.UnitId, simulation.Projectiles[0].TargetUnitId);
            Assert.AreEqual(replacementTarget.UnitId, FindEvent(events, BattleEventType.UnitTargetChanged).TargetUnitId);
        }

        [Test]
        public void Longshot_ExecutesTargetBelowThresholdAtImpact()
        {
            BattleSimulation simulation = CreateLongshotSimulation(twoEnemies: false);
            UnitRuntimeState attacker = simulation.Units[0];
            UnitRuntimeState target = simulation.Units[1];
            attacker.CurrentMana = attacker.CombatSpec.ManaThreshold;
            target.CurrentHp = 199;
            var loop = new BattleTickLoop(simulation, TickDuration);

            loop.Tick(new BattleEventQueue());
            loop.Tick(new BattleEventQueue());
            var events = new BattleEventQueue();

            ProjectileResolutionResult result = ProjectileResolver.ResolveProjectiles(simulation, 5f, events);

            Assert.AreEqual(1, result.Hits);
            Assert.AreEqual(199, result.TotalDamage);
            Assert.IsTrue(target.IsDefeated);
            Assert.AreEqual(0, target.CurrentHp);
            Assert.AreEqual(199, FindEvent(events, BattleEventType.UnitDamaged).Amount);
            AssertEvent(events, BattleEventType.UnitDied);
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

            Assert.AreEqual(UnitSpecialPhase.Casting, attacker.SpecialPhase);
            Assert.AreEqual(0, attacker.CurrentMana);
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
            float effectDelay,
            float castDuration = 0f,
            BattleRuntimeTuning? tuning = null,
            float attacksPerSecond = 0.1f)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("attacker", 1);
            attacker.AttacksPerSecond = attacksPerSecond;
            attacker.ManaThreshold = 10;
            attacker.ManaPerSecond = 12;
            attacker.Special = CreateHasteBurstSpecial(effectDelay, castDuration);
            UnitDefinition target = TestDefinitions.CreateUnit("target", 1);
            target.AttacksPerSecond = 1f / 999f;
            target.ManaPerSecond = 12;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
                },
                tuning ?? BattleRuntimeTuning.Default);
        }

        private static BattleSimulation CreateFurySimulation(
            int targetHp,
            HexCoord? targetHex = null,
            bool includeReplacementTarget = false)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("fury-attacker", 100);
            attacker.Attack = 100;
            attacker.AttacksPerSecond = 0.1f;
            attacker.ManaThreshold = 10;
            attacker.ManaPerSecond = 0;
            attacker.Special = CreateFurySwipesSpecial();
            UnitDefinition target = TestDefinitions.CreateUnit("fury-target", 1);
            target.MaxHp = targetHp;
            target.AttacksPerSecond = 1f / 999f;
            if (!includeReplacementTarget)
            {
                return BattleSimulation.Create(
                    new HexBoard(5, 6, 1f),
                    new[]
                    {
                        new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                        new UnitSpawnData(2, target, BattleSide.Enemy, targetHex ?? new HexCoord(2, 1))
                    });
            }

            UnitDefinition replacement = TestDefinitions.CreateUnit("fury-replacement", 1);
            replacement.MaxHp = targetHp;
            replacement.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, targetHex ?? new HexCoord(2, 1)),
                    new UnitSpawnData(3, replacement, BattleSide.Enemy, new HexCoord(1, 2))
                });
        }

        private static BattleSimulation CreateMegaArrowSimulation(
            int targetHp,
            HexCoord? targetHex = null,
            bool includeReplacementTarget = false)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("mega-arrow-attacker", 1, UnitType.Range);
            attacker.MaxHp = 1000;
            attacker.Attack = 100;
            attacker.AttackRange = 2;
            attacker.AttacksPerSecond = 0.001f;
            attacker.ManaThreshold = 10;
            attacker.ManaPerSecond = 0;
            attacker.Special = CreateMegaArrowSpecial();
            UnitDefinition target = TestDefinitions.CreateUnit("mega-arrow-target", 1);
            target.MaxHp = targetHp;
            target.AttacksPerSecond = 1f / 999f;
            if (!includeReplacementTarget)
            {
                return BattleSimulation.Create(
                    new HexBoard(5, 6, 1f),
                    new[]
                    {
                        new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                        new UnitSpawnData(2, target, BattleSide.Enemy, targetHex ?? new HexCoord(2, 1))
                    });
            }

            UnitDefinition replacement = TestDefinitions.CreateUnit("mega-arrow-replacement", 1);
            replacement.MaxHp = targetHp;
            replacement.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                    new UnitSpawnData(2, target, BattleSide.Enemy, targetHex ?? new HexCoord(2, 1)),
                    new UnitSpawnData(3, replacement, BattleSide.Enemy, new HexCoord(3, 1))
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

        private static BattleSimulation CreateLongshotSimulation(bool twoEnemies)
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("longshot-attacker", 1, UnitType.Range);
            attacker.MaxHp = 1000;
            attacker.Attack = 100;
            attacker.AttackRange = 1;
            attacker.AttacksPerSecond = 0.001f;
            attacker.ManaThreshold = 10;
            attacker.ManaPerSecond = 0;
            attacker.Special = CreateLongshotSpecial();
            UnitDefinition firstEnemy = CreatePassiveUnit("longshot-first-enemy");
            UnitDefinition secondEnemy = CreatePassiveUnit("longshot-second-enemy");
            return BattleSimulation.Create(
                new HexBoard(6, 6, 1f),
                twoEnemies
                    ? new[]
                    {
                        new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                        new UnitSpawnData(2, firstEnemy, BattleSide.Enemy, new HexCoord(3, 1)),
                        new UnitSpawnData(3, secondEnemy, BattleSide.Enemy, new HexCoord(4, 1))
                    }
                    : new[]
                    {
                        new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                        new UnitSpawnData(2, firstEnemy, BattleSide.Enemy, new HexCoord(3, 1))
                    });
        }

        private static UnitDefinition CreateSlamUnit(string unitId)
        {
            UnitDefinition unit = TestDefinitions.CreateUnit(unitId, 1);
            unit.MaxHp = 1000;
            unit.Attack = 100;
            unit.AttacksPerSecond = 0.001f;
            unit.ManaThreshold = 10;
            unit.ManaPerSecond = 0;
            unit.Special = CreateSlamSpecial();
            return unit;
        }

        private static UnitDefinition CreatePassiveUnit(string unitId)
        {
            UnitDefinition unit = TestDefinitions.CreateUnit(unitId, 1);
            unit.MaxHp = 1000;
            unit.AttacksPerSecond = 0.001f;
            unit.ManaPerSecond = 0;
            return unit;
        }

        private static UnitSpecialDefinition CreateHasteBurstSpecial(float effectDelay, float castDuration = 0f)
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.HasteBurst;
            special.EffectDelay = effectDelay;
            special.CastDuration = castDuration > 0f ? castDuration : effectDelay;
            special.AppliedStatus = CreateHasteStatus(5f, 0.5f);
            return special;
        }

        private static UnitSpecialDefinition CreateFurySwipesSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.FurySwipes;
            special.EffectDelay = 0f;
            special.CastDuration = 1.5f;
            special.StrikeCount = 10;
            special.AttackDamageMultiplier = 0.7f;
            return special;
        }

        private static UnitSpecialDefinition CreateSlamSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.Slam;
            special.EffectDelay = TickDuration;
            special.CastDuration = TickDuration;
            special.AttackDamageMultiplier = 1f;
            special.EffectRadius = 1;
            return special;
        }

        private static UnitSpecialDefinition CreateMegaArrowSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.MegaArrow;
            special.EffectDelay = TickDuration;
            special.CastDuration = 0.5f;
            special.AttackDamageMultiplier = 1.5f;
            special.AppliedStatus = CreateStunStatus(2f);
            special.AppliedStatusLifetimeMode = StatusLifetimeMode.OverrideSeconds;
            special.AppliedStatusDurationOverride = 1f;
            special.Projectile = CreateProjectile("mega-arrow", 1f);
            return special;
        }

        private static UnitSpecialDefinition CreateLongshotSpecial()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.Kind = UnitSpecialKind.Longshot;
            special.EffectDelay = TickDuration;
            special.CastDuration = 0.5f;
            special.AttackDamageMultiplier = 1.5f;
            special.ExecuteHpThresholdPercent = 20;
            special.Projectile = CreateProjectile("longshot", 1f);
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

        private static BattleEvent FindEvent(BattleEventQueue events, BattleEventType type)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected event: " + type);
            return default;
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

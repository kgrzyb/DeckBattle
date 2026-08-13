using System;
using Unity.Profiling;

namespace DeckBattle
{
    public static class DamageResolver
    {
        private static readonly ProfilerMarker ResolveMarker = new ProfilerMarker("DeckBattle.Damage.Resolve");

        public static HitResolutionResult Resolve(BattleSimulation simulation, UnitRuntimeState target, DamageRequest request, BattleEventQueue eventQueue)
        {
            using (ResolveMarker.Auto())
            {
                if (simulation == null) throw new ArgumentNullException(nameof(simulation));
                if (target == null || !target.IsAlive) return default;
                if (request.IsCritical)
                {
                    eventQueue?.Enqueue(BattleEvent.UnitCrit(request.Source != null ? request.Source.UnitId : 0, target.UnitId));
                }

                return ResolveInternal(simulation, target, request, eventQueue);
            }
        }

        private static HitResolutionResult ResolveInternal(BattleSimulation simulation, UnitRuntimeState target, DamageRequest request, BattleEventQueue eventQueue)
        {
            if (target == null || !target.IsAlive) return default;
            if (!request.BypassesGuard && !request.IsRedirected && IsGuardable(request.Kind) && TryGetGuard(simulation, target, out UnitRuntimeState guard))
            {
                int guardAmount = Math.Max(0, request.Amount) / 2;
                int targetAmount = Math.Max(0, request.Amount) - guardAmount;
                HitResolutionResult guardResult = ResolveInternal(
                    simulation,
                    guard,
                    new DamageRequest(request.Source, guardAmount, request.Kind, request.IsCritical, true, true, false, false),
                    eventQueue);
                eventQueue?.Enqueue(BattleEvent.DamageRedirected(target.UnitId, guard.UnitId, guardAmount));
                HitResolutionResult targetResult = ResolveInternal(
                    simulation,
                    target,
                    new DamageRequest(request.Source, targetAmount, request.Kind, request.IsCritical, true, true, request.CanTriggerMark, false),
                    eventQueue);
                if (request.CanApplyLifesteal && request.Kind == DamageKind.Direct) ApplyLifesteal(request.Source, guardResult.Damage + targetResult.Damage, eventQueue);
                return new HitResolutionResult(targetResult.DidHit, guardResult.Damage + targetResult.Damage, targetResult.Died);
            }

            int markDamage = request.CanTriggerMark && request.Kind == DamageKind.Direct ? ConsumeMark(target, request.Source, eventQueue) : 0;
            if (target.StatusSnapshot.Invulnerable) return new HitResolutionResult(true, 0, false);

            int remaining = Math.Max(0, request.Amount);
            if (target.StatusSnapshot.Exposed > 0f)
            {
                remaining = Math.Max(0, (int)Math.Round(remaining * (1f + target.StatusSnapshot.Exposed), MidpointRounding.AwayFromZero));
            }
            remaining = AbsorbShields(target, remaining, eventQueue);
            if (remaining <= 0)
            {
                ResolveMark(simulation, target, request.Source, markDamage, eventQueue);
                return new HitResolutionResult(true, 0, false);
            }

            target.CurrentHp -= remaining;
            eventQueue?.Enqueue(BattleEvent.UnitDamaged(
                target.UnitId,
                remaining,
                Math.Max(0, target.CurrentHp),
                target.CurrentHex,
                request.IsCritical));
            CombatResolver.GrantManaPulse(simulation, target, eventQueue);
            WakeSleep(target, eventQueue);
            bool died = target.CurrentHp <= 0;
            if (died)
            {
                simulation.DefeatUnit(target);
                SpecialCycleResolver.CancelWindup(target, eventQueue, simulation);
                eventQueue?.Enqueue(BattleEvent.UnitDied(target.UnitId));
            }
            if (request.CanApplyLifesteal && request.Kind == DamageKind.Direct) ApplyLifesteal(request.Source, remaining, eventQueue);
            if (!died) ResolveMark(simulation, target, request.Source, markDamage, eventQueue);
            return new HitResolutionResult(true, remaining, died);
        }

        private static bool IsGuardable(DamageKind kind)
        {
            return kind == DamageKind.Direct || kind == DamageKind.Mark;
        }

        private static bool TryGetGuard(BattleSimulation simulation, UnitRuntimeState target, out UnitRuntimeState guard)
        {
            guard = null;
            int oldestSequence = int.MaxValue;
            for (int i = 0; i < target.Statuses.Count; i++)
            {
                StatusInstance status = target.Statuses[i];
                if (status.Kind != StatusKind.Guard || status.ApplicationSequenceId >= oldestSequence) continue;
                if (!simulation.TryGetUnitById(status.LinkedUnitId, out UnitRuntimeState candidate)
                    || candidate == null
                    || !candidate.IsAlive
                    || candidate.Side != target.Side)
                {
                    continue;
                }

                guard = candidate;
                oldestSequence = status.ApplicationSequenceId;
            }

            return guard != null;
        }

        private static int ConsumeMark(UnitRuntimeState target, UnitRuntimeState source, BattleEventQueue eventQueue)
        {
            if (source == null || source.Side == target.Side) return 0;
            for (int i = 0; i < target.Statuses.Count; i++)
            {
                StatusInstance status = target.Statuses[i];
                if (status.Kind != StatusKind.Mark) continue;
                target.Statuses.RemoveAt(i);
                StatusResolver.RebuildSnapshot(target);
                eventQueue?.Enqueue(BattleEvent.StatusRemoved(target.UnitId, status.SourceUnitId, status.Kind, status.Stacks));
                return Math.Max(0, (int)Math.Round(status.Magnitude * status.Stacks, MidpointRounding.AwayFromZero));
            }
            return 0;
        }

        private static void ResolveMark(BattleSimulation simulation, UnitRuntimeState target, UnitRuntimeState source, int amount, BattleEventQueue eventQueue)
        {
            if (amount > 0 && target.IsAlive) Resolve(simulation, target, new DamageRequest(source, amount, DamageKind.Mark), eventQueue);
        }

        private static void ApplyLifesteal(UnitRuntimeState source, int hpDamage, BattleEventQueue eventQueue)
        {
            if (source == null || !source.IsAlive || hpDamage <= 0 || source.StatusSnapshot.Lifesteal <= 0f) return;
            int amount = (int)Math.Floor(hpDamage * Math.Min(1f, source.StatusSnapshot.Lifesteal));
            HealingResolver.Resolve(source, amount, eventQueue);
        }

        private static int AbsorbShields(UnitRuntimeState target, int damage, BattleEventQueue eventQueue)
        {
            int remaining = damage;
            while (remaining > 0 && target.StatusSnapshot.TotalShield > 0)
            {
                int selected = -1;
                double end = double.PositiveInfinity;
                int sequence = int.MaxValue;
                for (int i = 0; i < target.Statuses.Count; i++)
                {
                    StatusInstance instance = target.Statuses[i];
                    if (instance.Kind != StatusKind.Shield || instance.RemainingShield <= 0) continue;
                    if (instance.EndTime < end || (instance.EndTime == end && instance.ApplicationSequenceId < sequence)) { selected = i; end = instance.EndTime; sequence = instance.ApplicationSequenceId; }
                }
                if (selected < 0) break;
                StatusInstance shield = target.Statuses[selected];
                int absorbed = Math.Min(remaining, shield.RemainingShield);
                shield.RemainingShield -= absorbed;
                remaining -= absorbed;
                if (shield.RemainingShield == 0)
                {
                    target.Statuses.RemoveAt(selected);
                    eventQueue?.Enqueue(BattleEvent.StatusRemoved(target.UnitId, shield.SourceUnitId, StatusKind.Shield, shield.Stacks));
                }
                else target.Statuses.Set(selected, shield);
                StatusResolver.RebuildSnapshot(target);
                eventQueue?.Enqueue(BattleEvent.ShieldChanged(target.UnitId, target.StatusSnapshot.TotalShield));
            }
            return remaining;
        }

        private static void WakeSleep(UnitRuntimeState target, BattleEventQueue eventQueue)
        {
            for (int i = target.Statuses.Count - 1; i >= 0; i--)
            {
                StatusInstance instance = target.Statuses[i];
                if (instance.Kind != StatusKind.Sleep) continue;
                target.Statuses.RemoveAt(i);
                eventQueue?.Enqueue(BattleEvent.StatusRemoved(target.UnitId, instance.SourceUnitId, instance.Kind, instance.Stacks));
                StatusResolver.RebuildSnapshot(target);
                return;
            }
        }
    }
}

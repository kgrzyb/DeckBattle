using System;

namespace DeckBattle
{
    public static class StatusResolver
    {
        public static StatusApplicationResult TryApply(
            BattleSimulation simulation,
            UnitRuntimeState target,
            StatusApplicationRequest request,
            BattleEventQueue eventQueue = null)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (target == null || !target.IsAlive || request.Definition == null || request.Definition.Kind == StatusKind.None)
            {
                return Reject(target, request, StatusApplicationResult.RejectedInvalid, eventQueue);
            }

            StatusDefinition definition = request.Definition;
            if (definition.Kind == StatusKind.Drain || definition.StackingRule == StatusStackingRule.InstantOnly)
            {
                if (IsHarmful(definition.Category) && target.StatusSnapshot.Invulnerable)
                {
                    return Reject(target, request, StatusApplicationResult.RejectedInvulnerable, eventQueue);
                }

                int amount = Math.Max(0, (int)(request.Magnitude >= 0f ? request.Magnitude : definition.DefaultMagnitude));
                int drained = Math.Min(target.CurrentMana, amount);
                target.CurrentMana -= drained;
                eventQueue?.Enqueue(BattleEvent.UnitManaChanged(target.UnitId, target.CurrentMana));
                eventQueue?.Enqueue(BattleEvent.ManaDrained(target.UnitId, request.SourceUnitId, drained, target.CurrentMana));
                return StatusApplicationResult.Applied;
            }

            if (IsHarmful(definition.Category) && target.StatusSnapshot.Invulnerable)
            {
                return Reject(target, request, StatusApplicationResult.RejectedInvulnerable, eventQueue);
            }

            if (definition.Category == StatusCategory.HarmfulCrowdControl && target.StatusSnapshot.Fearless)
            {
                return Reject(target, request, StatusApplicationResult.RejectedFearless, eventQueue);
            }

            float duration = request.Duration >= 0f ? request.Duration : definition.DefaultDuration;
            if (duration <= 0f)
            {
                return Reject(target, request, StatusApplicationResult.RejectedInvalid, eventQueue);
            }

            float magnitude = request.Magnitude >= 0f ? request.Magnitude : definition.DefaultMagnitude;
            float interval = request.Interval >= 0f ? request.Interval : definition.DefaultInterval;
            int stacks = Math.Max(1, request.Stacks);
            double endTime = simulation.ElapsedTime + duration;

            if (definition.StackingRule != StatusStackingRule.IndependentShield
                && target.Statuses.TryFind(definition.Kind, request.SourceUnitId, out int existingIndex))
            {
                StatusInstance existing = target.Statuses[existingIndex];
                existing.EndTime = Math.Max(existing.EndTime, endTime);
                existing.Magnitude = Math.Max(existing.Magnitude, magnitude);
                existing.NextTickTime = interval > 0f ? simulation.ElapsedTime + interval : double.PositiveInfinity;
                existing.TickInterval = interval;
                if (definition.StackingRule == StatusStackingRule.AggregateStacks)
                {
                    existing.Stacks = Math.Min(definition.MaxStacks, existing.Stacks + stacks);
                }
                target.Statuses.Set(existingIndex, existing);
                RebuildSnapshot(target);
                eventQueue?.Enqueue(BattleEvent.StatusRefreshed(target.UnitId, request.SourceUnitId, definition.Kind, existing.Stacks, (float)(existing.EndTime - simulation.ElapsedTime)));
                return StatusApplicationResult.Refreshed;
            }

            var instance = new StatusInstance
            {
                Kind = definition.Kind,
                Category = definition.Category,
                StackingRule = definition.StackingRule,
                SourceUnitId = request.SourceUnitId,
                LinkedUnitId = request.LinkedUnitId,
                Magnitude = magnitude,
                Stacks = Math.Min(definition.MaxStacks, stacks),
                EndTime = endTime,
                NextTickTime = interval > 0f ? simulation.ElapsedTime + interval : double.PositiveInfinity,
                TickInterval = interval,
                RemainingShield = definition.Kind == StatusKind.Shield ? Math.Max(0, (int)magnitude) : 0
            };

            if (!target.Statuses.TryAdd(instance, out int index))
            {
                return Reject(target, request, StatusApplicationResult.CapacityReached, eventQueue);
            }

            if (definition.Kind == StatusKind.Stun || definition.Kind == StatusKind.Sleep)
            {
                AttackCycleResolver.CancelWindup(target, eventQueue);
            }

            if (definition.Kind == StatusKind.Invulnerability)
            {
                RemoveHarmfulStatuses(target, eventQueue);
            }
            else if (definition.Kind == StatusKind.Fearless)
            {
                RemoveCrowdControlStatuses(target, eventQueue);
            }

            RebuildSnapshot(target);
            StatusInstance applied = target.Statuses[index < target.Statuses.Count ? index : target.Statuses.Count - 1];
            eventQueue?.Enqueue(BattleEvent.StatusApplied(target.UnitId, request.SourceUnitId, definition.Kind, applied.Stacks, duration));
            return StatusApplicationResult.Applied;
        }

        public static void ExpireStatuses(BattleSimulation simulation, BattleEventQueue eventQueue = null)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            for (int unitIndex = 0; unitIndex < simulation.Units.Count; unitIndex++)
            {
                UnitRuntimeState unit = simulation.Units[unitIndex];
                if (unit == null) continue;
                bool changed = false;
                for (int statusIndex = unit.Statuses.Count - 1; statusIndex >= 0; statusIndex--)
                {
                    StatusInstance instance = unit.Statuses[statusIndex];
                    if (simulation.ElapsedTime < instance.EndTime) continue;
                    unit.Statuses.RemoveAt(statusIndex);
                    eventQueue?.Enqueue(BattleEvent.StatusRemoved(unit.UnitId, instance.SourceUnitId, instance.Kind, instance.Stacks));
                    changed = true;
                }
                if (changed) RebuildSnapshot(unit);
            }
        }

        public static void RebuildSnapshot(UnitRuntimeState unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            UnitStatusSnapshot snapshot = default;
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                StatusInstance instance = unit.Statuses[i];
                float totalMagnitude = instance.Magnitude * instance.Stacks;
                switch (instance.Kind)
                {
                    case StatusKind.Stun:
                    case StatusKind.Sleep:
                        snapshot.BlocksTargeting = true; snapshot.BlocksMovement = true; snapshot.BlocksAttack = true; snapshot.BlocksSpecial = true; break;
                    case StatusKind.Root: snapshot.BlocksMovement = true; break;
                    case StatusKind.Silence: snapshot.BlocksSpecial = true; break;
                    case StatusKind.Slow: snapshot.Slow += totalMagnitude; break;
                    case StatusKind.Haste: snapshot.Haste += totalMagnitude; break;
                    case StatusKind.Weaken: snapshot.Weaken += totalMagnitude; break;
                    case StatusKind.Empower: snapshot.Empower += totalMagnitude; break;
                    case StatusKind.Exposed: snapshot.Exposed += totalMagnitude; break;
                    case StatusKind.Shred: snapshot.Shred += totalMagnitude; break;
                    case StatusKind.Criticality: snapshot.Criticality += totalMagnitude; break;
                    case StatusKind.Lifesteal: snapshot.Lifesteal += totalMagnitude; break;
                    case StatusKind.Poison: snapshot.HealingReduction += totalMagnitude; break;
                    case StatusKind.Shield: snapshot.TotalShield += Math.Max(0, instance.RemainingShield); break;
                    case StatusKind.Invulnerability: snapshot.Invulnerable = true; break;
                    case StatusKind.Fearless: snapshot.Fearless = true; break;
                    case StatusKind.Untargetable: snapshot.Untargetable = true; break;
                }
            }
            unit.StatusSnapshot = snapshot;
            unit.StatusVersion++;
        }

        private static StatusApplicationResult Reject(UnitRuntimeState target, StatusApplicationRequest request, StatusApplicationResult reason, BattleEventQueue eventQueue)
        {
            eventQueue?.Enqueue(BattleEvent.StatusRejected(target != null ? target.UnitId : 0, request.SourceUnitId, request.Definition != null ? request.Definition.Kind : StatusKind.None, reason));
            return reason;
        }

        private static bool IsHarmful(StatusCategory category) { return category != StatusCategory.Beneficial; }

        private static void RemoveHarmfulStatuses(UnitRuntimeState target, BattleEventQueue eventQueue)
        {
            for (int i = target.Statuses.Count - 1; i >= 0; i--)
            {
                StatusInstance instance = target.Statuses[i];
                if (!IsHarmful(instance.Category) || instance.Kind == StatusKind.Invulnerability) continue;
                target.Statuses.RemoveAt(i);
                eventQueue?.Enqueue(BattleEvent.StatusRemoved(target.UnitId, instance.SourceUnitId, instance.Kind, instance.Stacks));
            }
        }

        private static void RemoveCrowdControlStatuses(UnitRuntimeState target, BattleEventQueue eventQueue)
        {
            for (int i = target.Statuses.Count - 1; i >= 0; i--)
            {
                StatusInstance instance = target.Statuses[i];
                if (instance.Category != StatusCategory.HarmfulCrowdControl) continue;
                target.Statuses.RemoveAt(i);
                eventQueue?.Enqueue(BattleEvent.StatusRemoved(target.UnitId, instance.SourceUnitId, instance.Kind, instance.Stacks));
            }
        }
    }
}

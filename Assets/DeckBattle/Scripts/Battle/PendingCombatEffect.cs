using System;

namespace DeckBattle
{
    public readonly struct PendingCombatEffectSpec
    {
        public readonly CombatEffectKind Kind;
        public readonly StatusCombatSpec StatusCombatSpec;
        public readonly StatusLifetimeMode LifetimeMode;
        public readonly float Duration;
        public readonly float Magnitude;
        public readonly float Interval;
        public readonly int Stacks;
        public readonly int Amount;
        public readonly float Percent;

        public PendingCombatEffectSpec(
            CombatEffectKind kind,
            StatusCombatSpec statusCombatSpec,
            StatusLifetimeMode lifetimeMode,
            float duration,
            float magnitude,
            float interval,
            int stacks,
            int amount,
            float percent)
        {
            Kind = kind;
            StatusCombatSpec = statusCombatSpec;
            LifetimeMode = lifetimeMode;
            Duration = duration;
            Magnitude = magnitude;
            Interval = interval;
            Stacks = Math.Max(1, stacks);
            Amount = Math.Max(0, amount);
            Percent = Math.Max(0f, percent);
        }

        public bool IsValid
        {
            get
            {
                return (Kind == CombatEffectKind.Status && StatusCombatSpec.Kind != StatusKind.None)
                    || (Kind == CombatEffectKind.ModifyBaseAttackPercent && Percent > 0f);
            }
        }

        public static bool TryCreate(CombatEffectDefinition definition, out PendingCombatEffectSpec spec)
        {
            switch (definition.Kind)
            {
                case CombatEffectKind.Status:
                    return TryCreateStatus(definition, out spec);
                case CombatEffectKind.ModifyBaseAttackPercent:
                    if (definition.Percent <= 0f)
                    {
                        spec = default(PendingCombatEffectSpec);
                        return false;
                    }

                    spec = new PendingCombatEffectSpec(
                        definition.Kind,
                        default,
                        StatusLifetimeMode.UseDefinitionDuration,
                        -1f,
                        -1f,
                        -1f,
                        1,
                        definition.Amount,
                        definition.Percent);
                    return true;
                default:
                    spec = default(PendingCombatEffectSpec);
                    return false;
            }
        }

        private static bool TryCreateStatus(CombatEffectDefinition definition, out PendingCombatEffectSpec spec)
        {
            if (definition.StatusApplication.Status == null)
            {
                spec = default(PendingCombatEffectSpec);
                return false;
            }

            StatusApplicationDefinition application = definition.StatusApplication;
            float duration = application.LifetimeMode == StatusLifetimeMode.OverrideSeconds
                ? application.DurationOverride
                : -1f;
            float magnitude = application.MagnitudeOverride > 0f ? application.MagnitudeOverride : -1f;
            float interval = application.IntervalOverride > 0f ? application.IntervalOverride : -1f;
            int stacks = application.StacksOverride > 0 ? application.StacksOverride : 1;
            spec = new PendingCombatEffectSpec(
                definition.Kind,
                StatusCombatSpec.FromDefinition(application.Status),
                application.LifetimeMode,
                duration,
                magnitude,
                interval,
                stacks,
                definition.Amount,
                0f);
            return true;
        }
    }

    public readonly struct PendingCombatEffect
    {
        public readonly int ApplicationSequenceId;
        public readonly int ScheduledRoundNumber;
        public readonly int SourceRuntimeUnitId;
        public readonly int TargetRuntimeUnitId;
        public readonly PendingCombatEffectSpec Spec;

        public PendingCombatEffect(
            int applicationSequenceId,
            int scheduledRoundNumber,
            int sourceRuntimeUnitId,
            int targetRuntimeUnitId,
            PendingCombatEffectSpec spec)
        {
            ApplicationSequenceId = applicationSequenceId;
            ScheduledRoundNumber = scheduledRoundNumber;
            SourceRuntimeUnitId = sourceRuntimeUnitId;
            TargetRuntimeUnitId = targetRuntimeUnitId;
            Spec = spec;
        }
    }

    public sealed class PendingCombatEffectQueue
    {
        private readonly PendingCombatEffect[] effects;
        private int count;
        private int nextApplicationSequenceId;

        public PendingCombatEffectQueue(int capacity)
        {
            effects = new PendingCombatEffect[Math.Max(1, capacity)];
        }

        public int Count { get { return count; } }
        public int Capacity { get { return effects.Length; } }
        public PendingCombatEffect this[int index] { get { return effects[index]; } }

        public bool CanReserve(int effectCount)
        {
            return effectCount >= 0 && effectCount <= effects.Length - count;
        }

        public bool TryEnqueue(int scheduledRoundNumber, int sourceRuntimeUnitId, int targetRuntimeUnitId, PendingCombatEffectSpec spec)
        {
            if (!CanReserve(1) || scheduledRoundNumber <= 0 || sourceRuntimeUnitId <= 0 || targetRuntimeUnitId <= 0 || !spec.IsValid)
            {
                return false;
            }

            effects[count++] = new PendingCombatEffect(
                ++nextApplicationSequenceId,
                scheduledRoundNumber,
                sourceRuntimeUnitId,
                targetRuntimeUnitId,
                spec);
            return true;
        }

        public void RollbackTo(int targetCount)
        {
            if (targetCount < 0 || targetCount > count)
            {
                throw new ArgumentOutOfRangeException(nameof(targetCount));
            }

            Array.Clear(effects, targetCount, count - targetCount);
            count = targetCount;
        }

        public void RemoveForRound(int roundNumber)
        {
            RemoveScheduledOnOrBefore(roundNumber);
        }

        public void RemoveBeforeRound(int roundNumber)
        {
            RemoveScheduledOnOrBefore(roundNumber - 1);
        }

        private void RemoveScheduledOnOrBefore(int maximumRoundNumber)
        {
            int destination = 0;
            for (int i = 0; i < count; i++)
            {
                PendingCombatEffect effect = effects[i];
                if (effect.ScheduledRoundNumber > maximumRoundNumber)
                {
                    effects[destination++] = effect;
                }
            }

            Array.Clear(effects, destination, count - destination);
            count = destination;
        }
    }

    public static class CombatEffectResolver
    {
        public static bool TryResolveInitial(BattleSimulation simulation, PendingCombatEffect effect)
        {
            if (simulation == null)
            {
                return false;
            }

            UnitRuntimeState source;
            UnitRuntimeState target;
            if (!simulation.TryGetUnitById(effect.SourceRuntimeUnitId, out source)
                || !simulation.TryGetUnitById(effect.TargetRuntimeUnitId, out target)
                || source == null
                || target == null)
            {
                return false;
            }

            switch (effect.Spec.Kind)
            {
                case CombatEffectKind.Status:
                    StatusApplicationResult result = StatusResolver.TryApply(
                        simulation,
                        target,
                        new StatusApplicationRequest(
                            effect.Spec.StatusCombatSpec,
                            source.UnitId,
                            effect.Spec.Duration,
                            effect.Spec.Magnitude,
                            effect.Spec.Interval,
                            effect.Spec.Stacks,
                            0,
                            effect.Spec.LifetimeMode));
                    return result == StatusApplicationResult.Applied || result == StatusApplicationResult.Refreshed;
                case CombatEffectKind.ModifyBaseAttackPercent:
                    target.BaseAttackBonusPercent += effect.Spec.Percent;
                    return true;
                default:
                    return false;
            }
        }
    }
}

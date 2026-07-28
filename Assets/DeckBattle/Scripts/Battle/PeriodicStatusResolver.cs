using System;
using Unity.Profiling;

namespace DeckBattle
{
    public static class PeriodicStatusResolver
    {
        private static readonly ProfilerMarker ResolveMarker = new ProfilerMarker("DeckBattle.Status.PeriodicTick");

        public static void Resolve(BattleSimulation simulation, BattleEventQueue eventQueue)
        {
            using (ResolveMarker.Auto())
            {
            for (int unitIndex = 0; unitIndex < simulation.Units.Count; unitIndex++)
            {
                UnitRuntimeState target = simulation.Units[unitIndex];
                if (target == null || !target.IsAlive) continue;
                for (int i = 0; i < target.Statuses.Count; i++)
                {
                    StatusInstance status = target.Statuses[i];
                    if (simulation.ElapsedTime < status.NextTickTime || status.NextTickTime > status.EndTime) continue;
                    if (status.Kind != StatusKind.Burn && status.Kind != StatusKind.Poison && status.Kind != StatusKind.Bleed && status.Kind != StatusKind.Regen) continue;
                    status.NextTickTime += Math.Max(0.01f, status.TickInterval);
                    target.Statuses.Set(i, status);
                    int amount = Math.Max(0, (int)Math.Round(status.Magnitude * status.Stacks, MidpointRounding.AwayFromZero));
                    if (status.Kind == StatusKind.Regen) HealingResolver.Resolve(target, amount, eventQueue);
                    else
                    {
                        UnitRuntimeState source = null;
                        simulation.TryGetUnitById(status.SourceUnitId, out source);
                        DamageResolver.Resolve(simulation, target, new DamageRequest(source, amount, DamageKind.DamageOverTime), eventQueue);
                    }
                    eventQueue?.Enqueue(BattleEvent.PeriodicEffectTicked(target.UnitId, status.SourceUnitId, status.Kind, amount));
                    if (!target.IsAlive) break;
                }
            }
            }
        }
    }
}

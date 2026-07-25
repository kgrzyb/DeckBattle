using System;

namespace DeckBattle
{
    public static class ProjectileResolver
    {
        public static ProjectileResolutionResult ResolveProjectiles(BattleSimulation simulation, float tickDuration, BattleEventQueue eventQueue)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (tickDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration));
            }

            // Compatibility entry point for standalone callers. The tick loop uses
            // the overload below after advancing simulation time exactly once.
            simulation.AdvanceTime(tickDuration);
            return ResolveProjectiles(simulation, eventQueue);
        }

        public static ProjectileResolutionResult ResolveProjectiles(BattleSimulation simulation, BattleEventQueue eventQueue)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            int hits = 0;
            int totalDamage = 0;
            int deaths = 0;

            int index = 0;
            while (index < simulation.Projectiles.Count)
            {
                ProjectileRuntimeState projectile = simulation.Projectiles[index];
                if (projectile == null)
                {
                    simulation.RemoveProjectileAt(index);
                    continue;
                }

                UnitRuntimeState target;
                bool targetAlive = simulation.TryGetUnitById(projectile.TargetUnitId, out target)
                    && target != null
                    && target.IsAlive;
                if (targetAlive)
                {
                    projectile.LastKnownTargetHex = target.CurrentHex;
                }

                if (simulation.ElapsedTime < projectile.ImpactTime)
                {
                    index++;
                    continue;
                }

                if (targetAlive)
                {
                    if (eventQueue != null)
                    {
                        eventQueue.Enqueue(BattleEvent.ProjectileResolved(
                            projectile.ProjectileId,
                            projectile.AttackerUnitId,
                            projectile.TargetUnitId,
                            projectile.LastKnownTargetHex,
                            true));
                        eventQueue.Enqueue(BattleEvent.ProjectileHit(
                            projectile.ProjectileId,
                            projectile.AttackerUnitId,
                            projectile.TargetUnitId,
                            projectile.LastKnownTargetHex));

                    }
                    HitResolutionResult hit = HitResolver.ResolveHit(simulation, null, target, projectile.Damage, projectile.IsCritical, eventQueue);
                    if (hit.DidHit) { hits++; totalDamage += hit.Damage; if (hit.Died) deaths++; }
                }
                else if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.ProjectileResolved(
                        projectile.ProjectileId,
                        projectile.AttackerUnitId,
                        projectile.TargetUnitId,
                        projectile.LastKnownTargetHex,
                        false));
                }

                simulation.RemoveProjectileAt(index);
            }

            return new ProjectileResolutionResult(hits, totalDamage, deaths);
        }
    }

    public readonly struct ProjectileResolutionResult
    {
        public readonly int Hits;
        public readonly int TotalDamage;
        public readonly int Deaths;

        public ProjectileResolutionResult(int hits, int totalDamage, int deaths)
        {
            Hits = hits;
            TotalDamage = totalDamage;
            Deaths = deaths;
        }
    }
}

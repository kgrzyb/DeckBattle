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

                projectile.TravelTimeRemaining = Math.Max(0f, projectile.TravelTimeRemaining - tickDuration);
                if (projectile.TravelTimeRemaining > 0f)
                {
                    index++;
                    continue;
                }

                if (targetAlive)
                {
                    if (eventQueue != null)
                    {
                        eventQueue.Enqueue(BattleEvent.ProjectileHit(
                            projectile.ProjectileId,
                            projectile.AttackerUnitId,
                            projectile.TargetUnitId,
                            projectile.LastKnownTargetHex));

                        if (projectile.IsCritical)
                        {
                            eventQueue.Enqueue(BattleEvent.UnitCrit(projectile.AttackerUnitId, projectile.TargetUnitId));
                        }
                    }

                    target.CurrentHp -= projectile.Damage;
                    hits++;
                    totalDamage += projectile.Damage;
                    if (eventQueue != null)
                    {
                        eventQueue.Enqueue(BattleEvent.UnitDamaged(target.UnitId, projectile.Damage, Math.Max(0, target.CurrentHp)));
                    }

                    if (projectile.Damage > 0)
                    {
                        CombatResolver.AddMana(target, target.Definition.ManaPerDamageTaken, eventQueue);
                    }

                    if (target.CurrentHp <= 0 && !target.IsDefeated)
                    {
                        simulation.DefeatUnit(target);
                        if (eventQueue != null)
                        {
                            eventQueue.Enqueue(BattleEvent.UnitDied(target.UnitId));
                        }

                        deaths++;
                    }
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

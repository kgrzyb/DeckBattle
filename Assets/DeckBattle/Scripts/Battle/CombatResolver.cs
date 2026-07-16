using System;

namespace DeckBattle
{
    public static class CombatResolver
    {
        public static CombatResolutionResult ResolveCombat(BattleSimulation simulation, float tickDuration)
        {
            return ResolveCombat(simulation, tickDuration, null);
        }

        public static CombatResolutionResult ResolveCombat(BattleSimulation simulation, float tickDuration, BattleEventQueue eventQueue)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (tickDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration));
            }

            int attacks = 0;
            int totalDamage = 0;
            int deaths = 0;

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState attacker = simulation.Units[i];
                if (attacker == null || !attacker.IsAlive)
                {
                    continue;
                }

                ReduceCooldown(attacker, tickDuration);

                UnitRuntimeState target;
                if (!TryGetLiveTarget(simulation, attacker, out target))
                {
                    continue;
                }

                if (simulation.Board.Distance(attacker.CurrentHex, target.CurrentHex) > simulation.Tuning.GetAttackRange(attacker.Definition))
                {
                    continue;
                }

                if (attacker.AttackCooldownRemaining > 0f)
                {
                    continue;
                }

                int damage = Math.Max(0, attacker.Definition.Attack);
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitAttackStarted(attacker.UnitId, target.UnitId));
                }

                target.CurrentHp -= damage;
                attacker.AttackCooldownRemaining = simulation.Tuning.GetAttackCooldown(attacker.Definition);
                attacks++;
                totalDamage += damage;
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitDamaged(target.UnitId, damage, Math.Max(0, target.CurrentHp)));
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

            return new CombatResolutionResult(attacks, totalDamage, deaths);
        }

        private static void ReduceCooldown(UnitRuntimeState unit, float tickDuration)
        {
            if (unit.AttackCooldownRemaining <= 0f || tickDuration <= 0f)
            {
                return;
            }

            unit.AttackCooldownRemaining = Math.Max(0f, unit.AttackCooldownRemaining - tickDuration);
        }

        private static bool TryGetLiveTarget(BattleSimulation simulation, UnitRuntimeState attacker, out UnitRuntimeState target)
        {
            target = null;
            if (attacker.TargetUnitId == UnitRuntimeState.NoTargetUnitId)
            {
                return false;
            }

            if (!simulation.TryGetUnitById(attacker.TargetUnitId, out target) || target == null || !target.IsAlive)
            {
                attacker.ClearTarget();
                target = null;
                return false;
            }

            if (target.Side == attacker.Side)
            {
                attacker.ClearTarget();
                target = null;
                return false;
            }

            return true;
        }
    }

    public readonly struct CombatResolutionResult
    {
        public readonly int Attacks;
        public readonly int TotalDamage;
        public readonly int Deaths;

        public CombatResolutionResult(int attacks, int totalDamage, int deaths)
        {
            Attacks = attacks;
            TotalDamage = totalDamage;
            Deaths = deaths;
        }
    }
}

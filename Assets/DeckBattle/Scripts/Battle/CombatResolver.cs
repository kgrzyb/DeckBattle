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
                UpdateSpecialDuration(attacker, tickDuration);
                if (attacker.IsMoving)
                {
                    continue;
                }

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

                bool isCritical;
                int damage = DamageCalculator.CalculateDamage(attacker.Definition, target.Definition, simulation.Random, out isCritical);
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitAttackStarted(attacker.UnitId, target.UnitId));
                    if (isCritical)
                    {
                        eventQueue.Enqueue(BattleEvent.UnitCrit(attacker.UnitId, target.UnitId));
                    }
                }

                target.CurrentHp -= damage;
                attacks++;
                totalDamage += damage;
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitDamaged(target.UnitId, damage, Math.Max(0, target.CurrentHp)));
                }

                AddMana(attacker, attacker.Definition.ManaPerAttack, eventQueue);
                if (damage > 0)
                {
                    AddMana(target, target.Definition.ManaPerDamageTaken, eventQueue);
                }
                attacker.AttackCooldownRemaining = simulation.Tuning.GetAttackCooldown(attacker.Definition, attacker);

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

        private static void UpdateSpecialDuration(UnitRuntimeState unit, float tickDuration)
        {
            if (unit.SpecialDurationRemaining <= 0f || tickDuration <= 0f)
            {
                return;
            }

            unit.SpecialDurationRemaining = Math.Max(0f, unit.SpecialDurationRemaining - tickDuration);
            if (unit.SpecialDurationRemaining <= 0f)
            {
                unit.AttackCooldownMultiplier = 1f;
            }
        }

        private static void AddMana(UnitRuntimeState unit, int amount, BattleEventQueue eventQueue)
        {
            if (unit == null || amount <= 0 || !unit.IsAlive)
            {
                return;
            }

            int threshold = unit.Definition.ManaThreshold;
            unit.CurrentMana = Math.Max(0, unit.CurrentMana + amount);
            if (eventQueue != null)
            {
                eventQueue.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
            }

            if (threshold <= 0 || unit.CurrentMana < threshold)
            {
                return;
            }

            unit.CurrentMana = 0;
            unit.AttackCooldownMultiplier = 0.5f;
            unit.SpecialDurationRemaining = 5f;
            if (eventQueue != null)
            {
                eventQueue.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
                eventQueue.Enqueue(BattleEvent.UnitSpecialActivated(unit.UnitId, unit.SpecialDurationRemaining));
            }
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

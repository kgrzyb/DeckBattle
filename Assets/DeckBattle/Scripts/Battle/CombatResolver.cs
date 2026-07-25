using System;

namespace DeckBattle
{
    public static class CombatResolver
    {
        public static CombatResolutionResult ResolveCombat(BattleSimulation simulation)
        {
            return ResolveCombat(simulation, null);
        }

        public static CombatResolutionResult ResolveCombat(BattleSimulation simulation, BattleEventQueue eventQueue)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            return ResolveCombat(simulation, eventQueue, new Workspace(simulation.Units.Count));
        }

        public static CombatResolutionResult ResolveCombat(BattleSimulation simulation, BattleEventQueue eventQueue, Workspace workspace)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.Clear();
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

                if (simulation.ElapsedTime < attacker.NextAttackTime)
                {
                    continue;
                }

                bool isCritical;
                int attackBonus = attacker.AttackBonusNextCombat;
                int damage = DamageCalculator.CalculateDamage(attacker.Definition, target.Definition, attackBonus, simulation.Random, out isCritical);
                attacker.AttackBonusNextCombat = 0;
                workspace.Add(new AttackIntent(attacker, target, damage, isCritical));
            }

            for (int i = 0; i < workspace.Count; i++)
            {
                AttackIntent intent = workspace[i];
                UnitRuntimeState attacker = intent.Attacker;
                UnitRuntimeState target = intent.Target;
                if (attacker == null || target == null || target.IsDefeated || target.CurrentHp <= 0)
                {
                    if (attacker != null)
                    {
                        attacker.ClearTarget();
                    }

                    continue;
                }

                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitAttackStarted(attacker.UnitId, target.UnitId));
                }

                attacks++;
                ProjectileDefinition projectileDefinition = attacker.Definition.Projectile;
                bool useProjectile = attacker.Definition.UnitType == UnitType.Range && projectileDefinition != null;
                if (useProjectile)
                {
                    ProjectileRuntimeState projectile = simulation.SpawnProjectile(attacker, target, projectileDefinition, intent.Damage, intent.IsCritical);
                    if (eventQueue != null)
                    {
                        eventQueue.Enqueue(BattleEvent.ProjectileLaunched(
                            projectile.ProjectileId,
                            attacker.UnitId,
                            target.UnitId,
                            projectile.FromHex,
                            projectile.LastKnownTargetHex,
                            projectile.TravelDuration));
                    }
                }
                else
                {
                    if (eventQueue != null && intent.IsCritical)
                    {
                        eventQueue.Enqueue(BattleEvent.UnitCrit(attacker.UnitId, target.UnitId));
                    }

                    target.CurrentHp -= intent.Damage;
                    totalDamage += intent.Damage;
                    if (eventQueue != null)
                    {
                        eventQueue.Enqueue(BattleEvent.UnitDamaged(target.UnitId, intent.Damage, Math.Max(0, target.CurrentHp)));
                    }

                    if (intent.Damage > 0)
                    {
                        AddMana(simulation, target, target.Definition.ManaPerDamageTaken, eventQueue);
                    }
                }

                AddMana(simulation, attacker, attacker.Definition.ManaPerAttack, eventQueue);
                workspace.AddCooldownReset(attacker);
            }

            for (int i = 0; i < workspace.CooldownResetCount; i++)
            {
                UnitRuntimeState attacker = workspace.GetCooldownResetUnit(i);
                if (attacker == null)
                {
                    continue;
                }

                attacker.NextAttackTime += simulation.Tuning.GetAttackCooldown(attacker.Definition, attacker);
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.IsDefeated || unit.CurrentHp > 0)
                {
                    continue;
                }

                simulation.DefeatUnit(unit);
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitDied(unit.UnitId));
                }

                deaths++;
            }

            return new CombatResolutionResult(attacks, totalDamage, deaths);
        }

        internal static void AddMana(BattleSimulation simulation, UnitRuntimeState unit, int amount, BattleEventQueue eventQueue)
        {
            if (unit == null || amount <= 0 || unit.IsDefeated)
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
            if (eventQueue != null)
            {
                eventQueue.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
            }

            ActivateSpecial(simulation, unit, eventQueue);
        }

        private static void ActivateSpecial(BattleSimulation simulation, UnitRuntimeState unit, BattleEventQueue eventQueue)
        {
            UnitSpecialDefinition special = unit.Definition.Special;
            if (special == null || special.Kind != UnitSpecialKind.AttackSpeed || special.Duration <= 0f)
            {
                return;
            }

            unit.ActiveSpecial = special;
            unit.SpecialEndTime = simulation.ElapsedTime + special.Duration;
            unit.SpecialAttackCooldownMultiplier = Math.Max(0.01f, special.AttackCooldownMultiplier);
            if (eventQueue != null)
            {
                eventQueue.Enqueue(BattleEvent.UnitSpecialActivated(unit.UnitId, special.Kind, special.Duration));
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

        public sealed class Workspace
        {
            private AttackIntent[] attackIntents;
            private UnitRuntimeState[] cooldownResetUnits;

            public Workspace(int capacity)
            {
                int resolvedCapacity = Math.Max(1, capacity);
                attackIntents = new AttackIntent[resolvedCapacity];
                cooldownResetUnits = new UnitRuntimeState[resolvedCapacity];
            }

            public int Count { get; private set; }
            public int CooldownResetCount { get; private set; }

            public AttackIntent this[int index]
            {
                get { return attackIntents[index]; }
            }

            public UnitRuntimeState GetCooldownResetUnit(int index)
            {
                return cooldownResetUnits[index];
            }

            public void Clear()
            {
                Count = 0;
                CooldownResetCount = 0;
            }

            public void Add(AttackIntent intent)
            {
                if (Count >= attackIntents.Length)
                {
                    Array.Resize(ref attackIntents, attackIntents.Length * 2);
                }

                attackIntents[Count] = intent;
                Count++;
            }

            public void AddCooldownReset(UnitRuntimeState unit)
            {
                if (CooldownResetCount >= cooldownResetUnits.Length)
                {
                    Array.Resize(ref cooldownResetUnits, cooldownResetUnits.Length * 2);
                }

                cooldownResetUnits[CooldownResetCount] = unit;
                CooldownResetCount++;
            }
        }

        public readonly struct AttackIntent
        {
            public readonly UnitRuntimeState Attacker;
            public readonly UnitRuntimeState Target;
            public readonly int Damage;
            public readonly bool IsCritical;

            public AttackIntent(UnitRuntimeState attacker, UnitRuntimeState target, int damage, bool isCritical)
            {
                Attacker = attacker;
                Target = target;
                Damage = damage;
                IsCritical = isCritical;
            }
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

using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleStateCombatTickLoop
    {
        private readonly BattleState battleState;
        private readonly float tickDuration;
        private readonly int maxTicks;
        private readonly MovementService.MovementWorkspace movementWorkspace;
        private readonly List<RuntimeUnit> allUnits;
        private readonly List<CombatantState> combatants;

        private int ticks;

        public BattleStateCombatTickLoop(BattleState battleState, float tickDuration, int maxTicks)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (tickDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration));
            }

            if (maxTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicks));
            }

            this.battleState = battleState;
            this.tickDuration = tickDuration;
            this.maxTicks = maxTicks;
            movementWorkspace = new MovementService.MovementWorkspace(battleState.Board.Width * battleState.Board.Height);

            int unitCapacity = battleState.Player.Units.Count + battleState.Enemy.Units.Count;
            allUnits = new List<RuntimeUnit>(unitCapacity);
            combatants = new List<CombatantState>(unitCapacity);
            AddUnits(battleState.Player.Units);
            AddUnits(battleState.Enemy.Units);
            combatants.Sort(CompareCombatants);
        }

        public CombatSimulationResult Tick(BattleEventQueue eventQueue)
        {
            if (eventQueue == null)
            {
                throw new ArgumentNullException(nameof(eventQueue));
            }

            eventQueue.Clear();

            CombatSimulationResult endedResult;
            if (TryCreateEndedResult(out endedResult))
            {
                return endedResult;
            }

            ticks++;
            ReduceCooldowns();

            for (int i = 0; i < combatants.Count; i++)
            {
                CombatantState combatant = combatants[i];
                RuntimeUnit unit = combatant.Unit;
                if (!unit.IsAlive)
                {
                    continue;
                }

                PlayerBattleState opponent = unit.Side == BattleSide.Player ? battleState.Enemy : battleState.Player;
                RuntimeUnit target = TargetingService.FindNearestTarget(battleState.Board, unit, opponent);
                if (target == null)
                {
                    break;
                }

                int distance = battleState.Board.Distance(unit.BattleCoord, target.BattleCoord);
                if (distance <= unit.Definition.AttackRange)
                {
                    TryAttack(combatant, target, eventQueue);
                }
                else
                {
                    TryMove(unit, target, eventQueue);
                }

                if (TryCreateEndedResult(out endedResult))
                {
                    return endedResult;
                }
            }

            if (ticks >= maxTicks)
            {
                battleState.Phase = BattlePhase.RoundResolution;
                return CombatSimulationResult.MaxTicksReached(ticks);
            }

            return null;
        }

        private void AddUnits(List<RuntimeUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                RuntimeUnit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                allUnits.Add(unit);
                combatants.Add(new CombatantState(unit));
            }
        }

        private void ReduceCooldowns()
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                CombatantState combatant = combatants[i];
                if (combatant.AttackCooldownRemaining > 0f)
                {
                    combatant.AttackCooldownRemaining = Math.Max(0f, combatant.AttackCooldownRemaining - tickDuration);
                }
            }
        }

        private void TryAttack(CombatantState combatant, RuntimeUnit target, BattleEventQueue eventQueue)
        {
            if (combatant.AttackCooldownRemaining > 0f || !target.IsAlive)
            {
                return;
            }

            RuntimeUnit attacker = combatant.Unit;
            int damage = Math.Max(0, attacker.Definition.Attack);
            eventQueue.Enqueue(BattleEvent.UnitAttackStarted(attacker.RuntimeId, target.RuntimeId));

            target.CurrentHp -= damage;
            combatant.AttackCooldownRemaining = attacker.Definition.AttackCooldown;
            eventQueue.Enqueue(BattleEvent.UnitDamaged(target.RuntimeId, damage, Math.Max(0, target.CurrentHp)));

            if (target.CurrentHp <= 0 && !target.IsDefeated)
            {
                target.IsDefeated = true;
                eventQueue.Enqueue(BattleEvent.UnitDied(target.RuntimeId));
            }
        }

        private void TryMove(RuntimeUnit unit, RuntimeUnit target, BattleEventQueue eventQueue)
        {
            HexCoord from = unit.BattleCoord;
            if (!MovementService.MoveTowardsTarget(battleState.Board, unit, target, allUnits, movementWorkspace))
            {
                return;
            }

            eventQueue.Enqueue(BattleEvent.UnitMoved(unit.RuntimeId, from, unit.BattleCoord));
        }

        private bool TryCreateEndedResult(out CombatSimulationResult result)
        {
            bool playerAlive = HasLivingUnits(battleState.Player);
            bool enemyAlive = HasLivingUnits(battleState.Enemy);
            if (playerAlive && enemyAlive)
            {
                result = null;
                return false;
            }

            battleState.Phase = BattlePhase.RoundResolution;
            if (!playerAlive && !enemyAlive)
            {
                result = CombatSimulationResult.Ended(ticks, false, BattleSide.Player, CombatEndReason.BothSidesDefeated);
                return true;
            }

            BattleSide winner = playerAlive ? BattleSide.Player : BattleSide.Enemy;
            result = CombatSimulationResult.Ended(ticks, true, winner, CombatEndReason.OneSideDefeated);
            return true;
        }

        private static bool HasLivingUnits(PlayerBattleState player)
        {
            for (int i = 0; i < player.Units.Count; i++)
            {
                RuntimeUnit unit = player.Units[i];
                if (unit != null && unit.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareCombatants(CombatantState left, CombatantState right)
        {
            return left.Unit.RuntimeId.CompareTo(right.Unit.RuntimeId);
        }

        private sealed class CombatantState
        {
            public readonly RuntimeUnit Unit;
            public float AttackCooldownRemaining;

            public CombatantState(RuntimeUnit unit)
            {
                Unit = unit;
                AttackCooldownRemaining = 0f;
            }
        }
    }
}

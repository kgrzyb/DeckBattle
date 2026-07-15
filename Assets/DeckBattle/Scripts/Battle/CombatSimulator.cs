using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class CombatSimulator
    {
        public const float DefaultTickDuration = 0.25f;
        public const int DefaultMaxTicks = 1000;

        public static CombatSimulationResult Simulate(BattleState battleState)
        {
            return Simulate(battleState, DefaultTickDuration, DefaultMaxTicks);
        }

        public static CombatSimulationResult Simulate(BattleState battleState, float tickDuration, int maxTicks)
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

            var combatants = new List<CombatantState>(battleState.Player.Units.Count + battleState.Enemy.Units.Count);
            AddCombatants(combatants, battleState.Player.Units);
            AddCombatants(combatants, battleState.Enemy.Units);
            combatants.Sort(CompareCombatants);

            var allUnits = new List<RuntimeUnit>(combatants.Count);
            for (int i = 0; i < combatants.Count; i++)
            {
                allUnits.Add(combatants[i].Unit);
            }

            CombatSimulationResult startResult;
            if (TryCreateEndedResult(battleState, 0, out startResult))
            {
                return startResult;
            }

            for (int tick = 1; tick <= maxTicks; tick++)
            {
                ReduceCooldowns(combatants, tickDuration);

                for (int i = 0; i < combatants.Count; i++)
                {
                    RuntimeUnit unit = combatants[i].Unit;
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
                        if (combatants[i].AttackCooldownRemaining <= 0f)
                        {
                            DamageService.ApplyAttack(unit, target);
                            combatants[i].AttackCooldownRemaining = unit.Definition.AttackCooldown;
                        }
                    }
                    else
                    {
                        MovementService.MoveTowardsTarget(battleState.Board, unit, target, allUnits);
                    }

                    CombatSimulationResult actionResult;
                    if (TryCreateEndedResult(battleState, tick, out actionResult))
                    {
                        return actionResult;
                    }
                }

                CombatSimulationResult tickResult;
                if (TryCreateEndedResult(battleState, tick, out tickResult))
                {
                    return tickResult;
                }
            }

            battleState.Phase = BattlePhase.RoundResolution;
            return CombatSimulationResult.MaxTicksReached(maxTicks);
        }

        private static void AddCombatants(List<CombatantState> combatants, List<RuntimeUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                RuntimeUnit unit = units[i];
                if (unit != null)
                {
                    combatants.Add(new CombatantState(unit));
                }
            }
        }

        private static void ReduceCooldowns(List<CombatantState> combatants, float tickDuration)
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                if (combatants[i].AttackCooldownRemaining > 0f)
                {
                    combatants[i].AttackCooldownRemaining -= tickDuration;
                }
            }
        }

        private static int CompareCombatants(CombatantState left, CombatantState right)
        {
            return left.Unit.RuntimeId.CompareTo(right.Unit.RuntimeId);
        }

        private static bool TryCreateEndedResult(BattleState battleState, int ticks, out CombatSimulationResult result)
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

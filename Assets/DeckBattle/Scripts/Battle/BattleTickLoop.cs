using System;

namespace DeckBattle
{
    public sealed class BattleTickLoop
    {
        private readonly MovementResolver.Workspace movementWorkspace;
        private readonly TargetSelector.Workspace targetWorkspace;
        private readonly CombatResolver.Workspace combatWorkspace;
        private readonly TargetSelector.TargetSelection[] targetSelections;
        private readonly bool[] targetSelectionValid;

        public BattleTickLoop(BattleSimulation simulation, float tickDuration)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (tickDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration));
            }

            TickDuration = tickDuration;
            int boardCellCapacity = simulation.Board.Width * simulation.Board.Height;
            movementWorkspace = new MovementResolver.Workspace(boardCellCapacity, simulation.Units.Count);
            targetWorkspace = new TargetSelector.Workspace(boardCellCapacity);
            combatWorkspace = new CombatResolver.Workspace(simulation.Units.Count);
            targetSelections = new TargetSelector.TargetSelection[simulation.Units.Count];
            targetSelectionValid = new bool[simulation.Units.Count];
        }

        public float TickDuration { get; private set; }

        public BattleTickResult Tick(BattleSimulation simulation, BattleEventQueue eventQueue)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (eventQueue == null)
            {
                throw new ArgumentNullException(nameof(eventQueue));
            }

            eventQueue.Clear();

            if (simulation.IsBattleEnded)
            {
                return new BattleTickResult(0, 0, true, simulation.HasWinner, simulation.Winner);
            }

            // Commit completed logical steps before any range or target query.
            MovementResolver.AdvanceActiveMovements(simulation, TickDuration);
            ProjectileResolver.ResolveProjectiles(simulation, TickDuration, eventQueue);

            RefreshTargets(simulation);
            CombatResolutionResult combat = CombatResolver.ResolveCombat(simulation, TickDuration, eventQueue, combatWorkspace);

            // Melee deaths and projectile/attack side effects can invalidate targets
            // and occupied attack positions before the next movement plan.
            RefreshTargets(simulation);
            int moved = MovementResolver.ResolveMovement(
                simulation,
                0f,
                movementWorkspace,
                eventQueue,
                targetSelections,
                targetSelectionValid);

            BattleSide winner;
            bool hasWinner;
            bool ended = TryEndBattle(simulation, out winner, out hasWinner);
            if (ended)
            {
                simulation.CompleteBattle(winner, hasWinner);
                eventQueue.Enqueue(BattleEvent.BattleEnded(winner, hasWinner));
            }

            return new BattleTickResult(combat.Attacks, moved, ended, hasWinner, winner);
        }

        private void RefreshTargets(BattleSimulation simulation)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                targetSelectionValid[i] = false;
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive || unit.IsMoving)
                {
                    continue;
                }

                if (!TargetSelector.TrySelectTargetOrRetainCurrent(
                        simulation,
                        unit,
                        targetWorkspace,
                        out TargetSelector.TargetSelection selection))
                {
                    unit.ClearTarget();
                    continue;
                }

                targetSelections[i] = selection;
                targetSelectionValid[i] = true;
                unit.SetTarget(selection.Target);
            }
        }

        private static bool TryEndBattle(BattleSimulation simulation, out BattleSide winner, out bool hasWinner)
        {
            bool playerAlive = false;
            bool enemyAlive = false;

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (unit.Side == BattleSide.Player)
                {
                    playerAlive = true;
                }
                else
                {
                    enemyAlive = true;
                }
            }

            if (playerAlive && enemyAlive)
            {
                winner = BattleSide.Player;
                hasWinner = false;
                return false;
            }

            if (simulation.Projectiles.Count > 0)
            {
                winner = BattleSide.Player;
                hasWinner = false;
                return false;
            }

            if (playerAlive == enemyAlive)
            {
                winner = BattleSide.Player;
                hasWinner = false;
                return true;
            }

            winner = playerAlive ? BattleSide.Player : BattleSide.Enemy;
            hasWinner = true;
            return true;
        }
    }

    public readonly struct BattleTickResult
    {
        public readonly int Attacks;
        public readonly int Moves;
        public readonly bool BattleEnded;
        public readonly bool HasWinner;
        public readonly BattleSide Winner;

        public BattleTickResult(int attacks, int moves, bool battleEnded, bool hasWinner, BattleSide winner)
        {
            Attacks = attacks;
            Moves = moves;
            BattleEnded = battleEnded;
            HasWinner = hasWinner;
            Winner = winner;
        }
    }
}

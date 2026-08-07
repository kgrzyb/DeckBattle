namespace DeckBattle
{
    public static class UnitActionRules
    {
        public static bool CanAcquireTarget(UnitRuntimeState unit)
        {
            return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksTargeting;
        }

        public static bool CanStartMovement(UnitRuntimeState unit)
        {
            return CanStartMovement(null, unit);
        }

        public static bool CanStartMovement(BattleSimulation simulation, UnitRuntimeState unit)
        {
            if (unit == null
                || !unit.IsAlive
                || unit.StatusSnapshot.BlocksMovement
                || unit.SpecialPhase == UnitSpecialPhase.Windup
                || unit.SpecialPhase == UnitSpecialPhase.Casting)
            {
                return false;
            }

            if (HasChargedSpecial(unit) && IsSpecialRecoveryEndingThisTick(simulation, unit))
            {
                return false;
            }

            return !HasChargedSpecial(unit) || !CanStartSpecialWindup(simulation, unit);
        }

        public static bool CanStartAttackWindup(UnitRuntimeState unit)
        {
            return unit != null
                && unit.IsAlive
                && !unit.StatusSnapshot.BlocksAttack
                && !IsSpecialActive(unit)
                && !HasChargedSpecial(unit);
        }

        public static bool CanActivateSpecial(UnitRuntimeState unit)
        {
            return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksSpecial;
        }

        public static bool CanStartSpecialWindup(UnitRuntimeState unit)
        {
            return CanStartSpecialWindup(null, unit);
        }

        public static bool CanStartSpecialWindup(BattleSimulation simulation, UnitRuntimeState unit)
        {
            if (!HasChargedSpecial(unit)
                || !CanActivateSpecial(unit)
                || unit.IsMoving
                || unit.AttackPhase == UnitAttackPhase.Windup
                || unit.SpecialPhase != UnitSpecialPhase.Idle
                || IsSpecialRecoveryEndingThisTick(simulation, unit))
            {
                return false;
            }

            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            return special.Kind != UnitSpecialKind.FurySwipes
                || TryGetFuryTarget(simulation, unit, out _);
        }

        public static bool TryGetFuryTarget(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            out UnitRuntimeState target)
        {
            target = null;
            if (simulation == null
                || unit == null
                || unit.TargetUnitId == UnitRuntimeState.NoTargetUnitId
                || !simulation.TryGetUnitById(unit.TargetUnitId, out target)
                || target == null
                || target.Side == unit.Side
                || !CanBeSelectedAsTarget(target))
            {
                target = null;
                return false;
            }

            return simulation.Board.Distance(unit.CurrentHex, target.CurrentHex)
                <= simulation.Tuning.GetAttackRange(unit.CombatSpec);
        }

        public static bool CanBeSelectedAsTarget(UnitRuntimeState unit)
        {
            return unit != null && unit.IsAlive && !unit.StatusSnapshot.Untargetable;
        }

        public static bool IsSpecialActive(UnitRuntimeState unit)
        {
            return unit != null && unit.SpecialPhase != UnitSpecialPhase.Idle;
        }

        public static bool HasReadySpecial(UnitRuntimeState unit)
        {
            return HasChargedSpecial(unit);
        }

        public static bool HasChargedSpecial(UnitRuntimeState unit)
        {
            return unit != null
                && unit.IsAlive
                && unit.CombatSpec.HasSpecial
                && unit.CombatSpec.ManaThreshold > 0
                && unit.CurrentMana >= unit.CombatSpec.ManaThreshold;
        }

        private static bool IsSpecialRecoveryEndingThisTick(BattleSimulation simulation, UnitRuntimeState unit)
        {
            return simulation != null
                && unit != null
                && unit.LastSpecialRecoveryEndTime == simulation.ElapsedTime;
        }
    }
}

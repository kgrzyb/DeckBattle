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
                && unit.SpecialPhase != UnitSpecialPhase.Windup
                && unit.SpecialPhase != UnitSpecialPhase.Casting
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
            if (special.Kind == UnitSpecialKind.Longshot)
            {
                return TryGetLongshotTarget(simulation, unit, out _);
            }

            return !SpecialRequiresTarget(special.Kind)
                || TryGetTargetedSpecialTarget(simulation, unit, out _);
        }

        public static bool SpecialRequiresTarget(UnitSpecialKind specialKind)
        {
            return specialKind == UnitSpecialKind.FurySwipes
                || specialKind == UnitSpecialKind.MegaArrow;
        }

        public static bool SpecialLocksTarget(UnitSpecialKind specialKind)
        {
            return SpecialRequiresTarget(specialKind)
                || specialKind == UnitSpecialKind.Longshot;
        }

        public static bool TryGetFuryTarget(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            out UnitRuntimeState target)
        {
            return TryGetTargetedSpecialTarget(simulation, unit, out target);
        }

        public static bool TryGetTargetedSpecialTarget(
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

        public static bool TryGetLongshotTarget(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            out UnitRuntimeState target)
        {
            target = null;
            if (simulation == null || unit == null)
            {
                return false;
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState candidate = simulation.Units[i];
                if (!TargetingRules.CanBeTargeted(unit, candidate))
                {
                    continue;
                }

                if (target == null
                    || candidate.CurrentHp < target.CurrentHp
                    || (candidate.CurrentHp == target.CurrentHp && candidate.UnitId < target.UnitId))
                {
                    target = candidate;
                }
            }

            return target != null;
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

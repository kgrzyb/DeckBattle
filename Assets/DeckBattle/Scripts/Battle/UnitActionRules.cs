namespace DeckBattle
{
    public static class UnitActionRules
    {
        public static bool CanAcquireTarget(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksTargeting; }
        public static bool CanStartMovement(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksMovement && (unit.SpecialPhase != UnitSpecialPhase.Windup && unit.SpecialPhase != UnitSpecialPhase.Casting) && !HasReadySpecial(unit); }
        public static bool CanStartAttackWindup(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksAttack && !IsSpecialActive(unit) && !HasReadySpecial(unit); }
        public static bool CanActivateSpecial(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksSpecial; }
        public static bool CanStartSpecialWindup(UnitRuntimeState unit) { return HasReadySpecial(unit) && CanActivateSpecial(unit) && !unit.IsMoving && unit.AttackPhase != UnitAttackPhase.Windup && unit.SpecialPhase == UnitSpecialPhase.Idle; }
        public static bool CanBeSelectedAsTarget(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.Untargetable; }

        public static bool IsSpecialActive(UnitRuntimeState unit) { return unit != null && unit.SpecialPhase != UnitSpecialPhase.Idle; }

        public static bool HasReadySpecial(UnitRuntimeState unit)
        {
            if (unit == null || !unit.IsAlive || !unit.CombatSpec.HasSpecial)
            {
                return false;
            }

            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            return special.Kind == UnitSpecialKind.HasteBurst
                && special.AppliedStatus.Kind == StatusKind.Haste
                && unit.CombatSpec.ManaThreshold > 0
                && unit.CurrentMana >= unit.CombatSpec.ManaThreshold;
        }
    }
}

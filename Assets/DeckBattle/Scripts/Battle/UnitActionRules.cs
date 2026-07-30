namespace DeckBattle
{
    public static class UnitActionRules
    {
        public static bool CanAcquireTarget(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksTargeting; }
        public static bool CanStartMovement(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksMovement && unit.SpecialPhase != UnitSpecialPhase.Windup && !HasReadySpecial(unit); }
        public static bool CanStartAttackWindup(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksAttack && unit.SpecialPhase != UnitSpecialPhase.Windup && !HasReadySpecial(unit); }
        public static bool CanActivateSpecial(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksSpecial; }
        public static bool CanStartSpecialWindup(UnitRuntimeState unit) { return HasReadySpecial(unit) && CanActivateSpecial(unit) && !unit.IsMoving && unit.AttackPhase == UnitAttackPhase.AcquireReload && unit.SpecialPhase == UnitSpecialPhase.Idle; }
        public static bool CanBeSelectedAsTarget(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.Untargetable; }

        public static bool HasReadySpecial(UnitRuntimeState unit)
        {
            if (unit == null || !unit.IsAlive || unit.Definition == null)
            {
                return false;
            }

            UnitSpecialDefinition special = unit.Definition.Special;
            return special != null
                && special.Kind == UnitSpecialKind.HasteBurst
                && special.AppliedStatus != null
                && special.AppliedStatus.Kind == StatusKind.Haste
                && unit.Definition.ManaThreshold > 0
                && unit.CurrentMana >= unit.Definition.ManaThreshold;
        }
    }
}

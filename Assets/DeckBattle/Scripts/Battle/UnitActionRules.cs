namespace DeckBattle
{
    public static class UnitActionRules
    {
        public static bool CanAcquireTarget(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksTargeting; }
        public static bool CanStartMovement(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksMovement; }
        public static bool CanStartAttackWindup(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksAttack; }
        public static bool CanActivateSpecial(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.BlocksSpecial; }
        public static bool CanBeSelectedAsTarget(UnitRuntimeState unit) { return unit != null && unit.IsAlive && !unit.StatusSnapshot.Untargetable; }
    }
}

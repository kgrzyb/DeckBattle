namespace DeckBattle
{
    public struct UnitStatusSnapshot
    {
        public bool BlocksTargeting;
        public bool BlocksMovement;
        public bool BlocksAttack;
        public bool BlocksSpecial;
        public bool Invulnerable;
        public bool Fearless;
        public bool Untargetable;
        public float Slow;
        public float Haste;
        public float Weaken;
        public float Empower;
        public float Exposed;
        public float Shred;
        public float Criticality;
        public float HealingReduction;
        public float Lifesteal;
        public int TotalShield;
    }
}

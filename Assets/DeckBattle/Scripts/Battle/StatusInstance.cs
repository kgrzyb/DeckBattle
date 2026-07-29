namespace DeckBattle
{
    public struct StatusInstance
    {
        public StatusKind Kind;
        public StatusCategory Category;
        public StatusStackingRule StackingRule;
        public StatusValueCombinationRule IndependentPerSourceCombination;
        public int SourceUnitId;
        public int LinkedUnitId;
        public int ApplicationSequenceId;
        public float Magnitude;
        public int Stacks;
        public double EndTime;
        public double NextTickTime;
        public float TickInterval;
        public int RemainingShield;
    }
}

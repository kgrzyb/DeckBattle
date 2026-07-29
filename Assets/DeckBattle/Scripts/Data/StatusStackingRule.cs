namespace DeckBattle
{
    public enum StatusStackingRule
    {
        IndependentPerSource = 0,
        RefreshPerSource = 1,
        AggregateStacks = 2,
        IndependentShield = 3,
        InstantOnly = 4,
        AggregateStacksAcrossSources = 5
    }
}

namespace DeckBattle
{
    public sealed class CardRuntimeState
    {
        public readonly int RuntimeCardId;
        public readonly UnitDefinition Definition;
        public CardLocation Location;

        public CardRuntimeState(int runtimeCardId, UnitDefinition definition)
        {
            RuntimeCardId = runtimeCardId;
            Definition = definition;
            Location = CardLocation.Deck;
        }
    }
}

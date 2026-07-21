namespace DeckBattle
{
    public sealed class CardRuntimeState
    {
        public readonly int RuntimeCardId;
        public readonly CardDefinition Definition;
        public CardLocation Location;

        public CardRuntimeState(int runtimeCardId, CardDefinition definition)
        {
            RuntimeCardId = runtimeCardId;
            Definition = definition;
            Location = CardLocation.Deck;
        }

        public UnitDefinition UnitDefinition
        {
            get { return Definition as UnitDefinition; }
        }

        public SpellDefinition SpellDefinition
        {
            get { return Definition as SpellDefinition; }
        }
    }
}

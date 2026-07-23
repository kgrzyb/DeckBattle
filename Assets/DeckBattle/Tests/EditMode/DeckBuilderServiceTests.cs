using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class DeckBuilderServiceTests
    {
        private readonly DeckBuilderService service = new DeckBuilderService();

        [Test]
        public void CannotAddCardOutsideCatalog()
        {
            UnitDefinition owned = TestDefinitions.CreateUnit("owned", 1);
            CardCatalog catalog = TestDefinitions.CreateCatalog(new CardDefinition[] { owned }, new CardDefinition[] { owned }, new CardDefinition[0]);
            PlayerProfile profile = CreateProfile("owned");

            bool added = service.TryAddCard(profile, "missing", catalog, DeckBuilderRules.MvpDefault, out DeckBuildFailReason reason);

            Assert.IsFalse(added);
            Assert.AreEqual(DeckBuildFailReason.UnknownCard, reason);
            Assert.AreEqual(0, profile.ActiveDeckCardIds.Count);
        }

        [Test]
        public void CannotAddUnownedCard()
        {
            UnitDefinition owned = TestDefinitions.CreateUnit("owned", 1);
            UnitDefinition locked = TestDefinitions.CreateUnit("locked", 1);
            CardCatalog catalog = TestDefinitions.CreateCatalog(new CardDefinition[] { owned, locked }, new CardDefinition[] { owned }, new CardDefinition[0]);
            PlayerProfile profile = CreateProfile("owned");

            bool added = service.TryAddCard(profile, "locked", catalog, DeckBuilderRules.MvpDefault, out DeckBuildFailReason reason);

            Assert.IsFalse(added);
            Assert.AreEqual(DeckBuildFailReason.CardNotOwned, reason);
            Assert.AreEqual(0, profile.ActiveDeckCardIds.Count);
        }

        [Test]
        public void CannotAddDuplicateCard()
        {
            UnitDefinition owned = TestDefinitions.CreateUnit("owned", 1);
            CardCatalog catalog = TestDefinitions.CreateCatalog(new CardDefinition[] { owned }, new CardDefinition[] { owned }, new CardDefinition[0]);
            PlayerProfile profile = CreateProfile("owned");
            profile.ActiveDeckCardIds.Add("owned");

            bool added = service.TryAddCard(profile, "owned", catalog, DeckBuilderRules.MvpDefault, out DeckBuildFailReason reason);

            Assert.IsFalse(added);
            Assert.AreEqual(DeckBuildFailReason.AlreadyInDeck, reason);
            Assert.AreEqual(1, profile.ActiveDeckCardIds.Count);
        }

        [Test]
        public void CannotExceedMaxDeckSize()
        {
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            UnitDefinition second = TestDefinitions.CreateUnit("second", 1);
            CardCatalog catalog = TestDefinitions.CreateCatalog(new CardDefinition[] { first, second }, new CardDefinition[] { first, second }, new CardDefinition[0]);
            PlayerProfile profile = CreateProfile("first", "second");
            profile.ActiveDeckCardIds.Add("first");

            bool added = service.TryAddCard(profile, "second", catalog, new DeckBuilderRules(1, 1), out DeckBuildFailReason reason);

            Assert.IsFalse(added);
            Assert.AreEqual(DeckBuildFailReason.DeckFull, reason);
            Assert.AreEqual(1, profile.ActiveDeckCardIds.Count);
        }

        [Test]
        public void CanRemoveCardFromDeck()
        {
            PlayerProfile profile = CreateProfile("first");
            profile.ActiveDeckCardIds.Add("first");

            bool removed = service.TryRemoveCard(profile, "first");

            Assert.IsTrue(removed);
            Assert.AreEqual(0, profile.ActiveDeckCardIds.Count);
        }

        [Test]
        public void RemovingMissingCardDoesNotChangeDeck()
        {
            PlayerProfile profile = CreateProfile("first", "second");
            profile.ActiveDeckCardIds.Add("first");

            bool removed = service.TryRemoveCard(profile, "second");

            Assert.IsFalse(removed);
            Assert.AreEqual(1, profile.ActiveDeckCardIds.Count);
            Assert.AreEqual("first", profile.ActiveDeckCardIds[0]);
        }

        [Test]
        public void ValidationDetectsTooSmallDeck()
        {
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            CardCatalog catalog = TestDefinitions.CreateCatalog(new CardDefinition[] { first }, new CardDefinition[] { first }, new CardDefinition[0]);
            PlayerProfile profile = CreateProfile("first");
            profile.ActiveDeckCardIds.Add("first");

            DeckValidationResult result = service.ValidateDeck(profile, catalog, new DeckBuilderRules(2, 8));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.CardCount);
            Assert.AreEqual(DeckBuildFailReason.DeckTooSmall, result.Reason);
        }

        [Test]
        public void ValidationAcceptsValidDeck()
        {
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            UnitDefinition second = TestDefinitions.CreateUnit("second", 1);
            CardCatalog catalog = TestDefinitions.CreateCatalog(new CardDefinition[] { first, second }, new CardDefinition[] { first, second }, new CardDefinition[0]);
            PlayerProfile profile = CreateProfile("first", "second");
            profile.ActiveDeckCardIds.Add("first");
            profile.ActiveDeckCardIds.Add("second");

            DeckValidationResult result = service.ValidateDeck(profile, catalog, new DeckBuilderRules(2, 8));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(2, result.CardCount);
            Assert.AreEqual(0, result.MissingCardCount);
            Assert.AreEqual(0, result.DuplicateCardCount);
            Assert.AreEqual(DeckBuildFailReason.None, result.Reason);
        }

        private static PlayerProfile CreateProfile(params string[] ownedCardIds)
        {
            var profile = new PlayerProfile();
            for (int i = 0; i < ownedCardIds.Length; i++)
            {
                profile.UnlockedCardIds.Add(ownedCardIds[i]);
            }

            return profile;
        }
    }
}

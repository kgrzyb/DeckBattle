using System.Collections.Generic;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleStartDataBuilderTests
    {
        private readonly BattleStartDataBuilder builder = new BattleStartDataBuilder();

        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
            BattleSession.Clear();
        }

        [Test]
        public void Build_UsesActiveProfileDeck()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            UnitDefinition archer = TestDefinitions.CreateUnit("archer", 2);
            CardCatalog catalog = CreateCatalog(new[] { swordsman, archer }, new[] { swordsman });
            PlayerProfile profile = CreateProfile(new[] { "swordsman", "archer" }, new[] { "archer", "swordsman" });

            BattleStartData data = builder.Build(profile, catalog, BattleStartRules.MvpDefault, 77);

            Assert.AreSame(archer, data.PlayerDeck[0]);
            Assert.AreSame(swordsman, data.PlayerDeck[1]);
        }

        [Test]
        public void Build_FallsBackToDefaultDeckWhenActiveDeckIsEmpty()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            UnitDefinition guard = TestDefinitions.CreateUnit("guard", 1);
            CardCatalog catalog = CreateCatalog(new[] { swordsman, guard }, new[] { guard });
            PlayerProfile profile = CreateProfile(new[] { "swordsman", "guard" }, new string[0]);

            BattleStartData data = builder.Build(profile, catalog, BattleStartRules.MvpDefault, 12);

            Assert.AreEqual(1, data.PlayerDeck.Count);
            Assert.AreSame(guard, data.PlayerDeck[0]);
        }

        [Test]
        public void Build_FallsBackWhenActiveDeckReferencesMissingCard()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            UnitDefinition guard = TestDefinitions.CreateUnit("guard", 1);
            CardCatalog catalog = CreateCatalog(new[] { swordsman, guard }, new[] { guard });
            PlayerProfile profile = CreateProfile(new[] { "swordsman", "guard" }, new[] { "swordsman", "missing" });

            BattleStartData data = builder.Build(profile, catalog, BattleStartRules.MvpDefault, 12);

            Assert.AreEqual(1, data.PlayerDeck.Count);
            Assert.AreSame(guard, data.PlayerDeck[0]);
        }

        [Test]
        public void Build_CreatesEnemyDeckFromCatalogDefaultDeck()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            UnitDefinition guard = TestDefinitions.CreateUnit("guard", 1);
            CardCatalog catalog = CreateCatalog(new[] { swordsman, guard }, new[] { guard });
            PlayerProfile profile = CreateProfile(new[] { "swordsman", "guard" }, new[] { "swordsman" });

            BattleStartData data = builder.Build(profile, catalog, BattleStartRules.MvpDefault, 12);

            Assert.AreEqual(1, data.EnemyDeck.Count);
            Assert.AreSame(guard, data.EnemyDeck[0]);
        }

        [Test]
        public void Build_PreservesProvidedSeed()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            CardCatalog catalog = CreateCatalog(new[] { swordsman }, new[] { swordsman });
            PlayerProfile profile = CreateProfile(new[] { "swordsman" }, new[] { "swordsman" });

            BattleStartData data = builder.Build(profile, catalog, BattleStartRules.MvpDefault, 9981);

            Assert.AreEqual(9981, data.Seed);
        }

        [Test]
        public void Build_DoesNotReturnDuplicateCardsWhenDeckContainsDuplicates()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            UnitDefinition guard = TestDefinitions.CreateUnit("guard", 1);
            CardCatalog catalog = CreateCatalog(new[] { swordsman, guard }, new[] { guard });
            PlayerProfile profile = CreateProfile(new[] { "swordsman", "guard" }, new[] { "swordsman", "swordsman" });

            BattleStartData data = builder.Build(profile, catalog, BattleStartRules.MvpDefault, 12);

            Assert.AreEqual(1, data.PlayerDeck.Count);
            Assert.AreSame(guard, data.PlayerDeck[0]);
        }

        [Test]
        public void BattleSession_ConsumesAndClearsPendingStartData()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            BattleSession.PendingStartData = new BattleStartData(new[] { swordsman }, new[] { swordsman }, 5);

            bool consumed = BattleSession.TryConsumePendingStartData(out BattleStartData data);
            bool consumedAgain = BattleSession.TryConsumePendingStartData(out BattleStartData secondData);

            Assert.IsTrue(consumed);
            Assert.IsNotNull(data);
            Assert.IsFalse(consumedAgain);
            Assert.IsNull(secondData);
        }

        private static CardCatalog CreateCatalog(IReadOnlyList<CardDefinition> allCards, IReadOnlyList<CardDefinition> defaultDeck)
        {
            return TestDefinitions.CreateCatalog(allCards, allCards, defaultDeck);
        }

        private static PlayerProfile CreateProfile(IReadOnlyList<string> ownedCardIds, IReadOnlyList<string> activeDeckCardIds)
        {
            var profile = new PlayerProfile
            {
                UnlockedCardIds = new List<string>(ownedCardIds),
                ActiveDeckCardIds = new List<string>(activeDeckCardIds)
            };

            return profile;
        }
    }
}

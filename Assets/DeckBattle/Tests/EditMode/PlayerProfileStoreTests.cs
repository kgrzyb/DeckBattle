using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class PlayerProfileStoreTests
    {
        private readonly List<string> tempFiles = new List<string>(4);

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < tempFiles.Count; i++)
            {
                if (File.Exists(tempFiles[i]))
                {
                    File.Delete(tempFiles[i]);
                }
            }

            tempFiles.Clear();
        }

        [Test]
        public void CreateDefaultProfile_UsesStartingCollectionDefaultDeckAndZeroShards()
        {
            CardCatalog catalog = CreateCatalog();

            PlayerProfile profile = PlayerProfileStore.CreateDefaultProfile(catalog);

            CollectionAssert.AreEqual(new[] { "swordsman", "archer", "guard" }, profile.UnlockedCardIds);
            CollectionAssert.AreEqual(new[] { "swordsman", "guard" }, profile.ActiveDeckCardIds);
            Assert.AreEqual(0, profile.Shards);
            Assert.AreEqual(PlayerProfile.CurrentSaveVersion, profile.SaveVersion);
        }

        [Test]
        public void SaveAndLoad_RoundTripsProfile()
        {
            CardCatalog catalog = CreateCatalog();
            string savePath = CreateTempSavePath();
            var store = new PlayerProfileStore(savePath);
            var profile = new PlayerProfile
            {
                Shards = 25,
                UnlockedCardIds = new List<string> { "swordsman", "archer" },
                ActiveDeckCardIds = new List<string> { "archer" }
            };

            store.Save(profile);
            PlayerProfile loadedProfile = store.LoadOrCreateDefault(catalog);

            Assert.AreEqual(25, loadedProfile.Shards);
            CollectionAssert.AreEqual(new[] { "swordsman", "archer" }, loadedProfile.UnlockedCardIds);
            CollectionAssert.AreEqual(new[] { "archer" }, loadedProfile.ActiveDeckCardIds);
        }

        [Test]
        public void Validate_RemovesDuplicatesFromCollectionAndDeck()
        {
            CardCatalog catalog = CreateCatalog();
            var profile = new PlayerProfile
            {
                UnlockedCardIds = new List<string> { "swordsman", "swordsman", "guard" },
                ActiveDeckCardIds = new List<string> { "guard", "guard", "swordsman" }
            };

            PlayerProfileStore.Validate(profile, catalog);

            CollectionAssert.AreEqual(new[] { "swordsman", "guard" }, profile.UnlockedCardIds);
            CollectionAssert.AreEqual(new[] { "guard", "swordsman" }, profile.ActiveDeckCardIds);
        }

        [Test]
        public void Validate_RemovesCardsOutsideCatalog()
        {
            CardCatalog catalog = CreateCatalog();
            var profile = new PlayerProfile
            {
                UnlockedCardIds = new List<string> { "swordsman", "missing-card", "guard" },
                ActiveDeckCardIds = new List<string> { "missing-card", "guard" }
            };

            PlayerProfileStore.Validate(profile, catalog);

            CollectionAssert.AreEqual(new[] { "swordsman", "guard" }, profile.UnlockedCardIds);
            CollectionAssert.AreEqual(new[] { "guard" }, profile.ActiveDeckCardIds);
        }

        [Test]
        public void Validate_RemovesDeckCardsThatPlayerDoesNotOwn()
        {
            CardCatalog catalog = CreateCatalog();
            var profile = new PlayerProfile
            {
                UnlockedCardIds = new List<string> { "swordsman" },
                ActiveDeckCardIds = new List<string> { "swordsman", "archer" }
            };

            PlayerProfileStore.Validate(profile, catalog);

            CollectionAssert.AreEqual(new[] { "swordsman" }, profile.ActiveDeckCardIds);
        }

        [Test]
        public void Validate_RestoresDefaultDeckWhenSavedDeckIsEmpty()
        {
            CardCatalog catalog = CreateCatalog();
            var profile = new PlayerProfile
            {
                UnlockedCardIds = new List<string> { "swordsman", "guard" },
                ActiveDeckCardIds = new List<string>()
            };

            PlayerProfileStore.Validate(profile, catalog);

            CollectionAssert.AreEqual(new[] { "swordsman", "guard" }, profile.ActiveDeckCardIds);
        }

        [Test]
        public void Validate_RestoresPlayableDeckWhenSavedDeckIsInvalid()
        {
            CardCatalog catalog = CreateCatalog();
            var profile = new PlayerProfile
            {
                UnlockedCardIds = new List<string> { "archer" },
                ActiveDeckCardIds = new List<string> { "missing-card", "guard" }
            };

            PlayerProfileStore.Validate(profile, catalog);

            CollectionAssert.AreEqual(new[] { "archer" }, profile.ActiveDeckCardIds);
        }

        private string CreateTempSavePath()
        {
            string path = Path.Combine(Path.GetTempPath(), "deck-battle-profile-" + Path.GetRandomFileName() + ".json");
            tempFiles.Add(path);
            return path;
        }

        private static CardCatalog CreateCatalog()
        {
            UnitDefinition swordsman = TestDefinitions.CreateUnit("swordsman", 1);
            UnitDefinition archer = TestDefinitions.CreateUnit("archer", 2);
            UnitDefinition guard = TestDefinitions.CreateUnit("guard", 1);
            var allCards = new CardDefinition[] { swordsman, archer, guard };
            var startingCards = new CardDefinition[] { swordsman, archer, guard };
            var defaultDeck = new CardDefinition[] { swordsman, guard };
            return TestDefinitions.CreateCatalog(allCards, startingCards, defaultDeck);
        }
    }
}

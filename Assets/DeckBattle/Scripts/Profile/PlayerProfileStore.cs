using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeckBattle
{
    public sealed class PlayerProfileStore
    {
        private const string SaveFileName = "player-profile.json";

        private readonly string savePath;

        public PlayerProfileStore()
            : this(Path.Combine(Application.persistentDataPath, SaveFileName))
        {
        }

        public PlayerProfileStore(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("Save path cannot be empty.", nameof(savePath));
            }

            this.savePath = savePath;
        }

        public string SavePath
        {
            get { return savePath; }
        }

        public PlayerProfile LoadOrCreateDefault(CardCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!File.Exists(savePath))
            {
                return CreateDefaultProfile(catalog);
            }

            try
            {
                string json = File.ReadAllText(savePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateDefaultProfile(catalog);
                }

                PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(json);
                if (profile == null)
                {
                    return CreateDefaultProfile(catalog);
                }

                Validate(profile, catalog);
                return profile;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not load player profile. Creating a default profile. " + exception.Message);
                return CreateDefaultProfile(catalog);
            }
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(savePath, json);
        }

        public void ResetProfile()
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }

        public static PlayerProfile CreateDefaultProfile(CardCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var profile = new PlayerProfile
            {
                SaveVersion = PlayerProfile.CurrentSaveVersion,
                Shards = 0
            };

            catalog.GetStartingCollectionCardIds(profile.UnlockedCardIds);
            RestoreDefaultDeck(profile, catalog);
            return profile;
        }

        public static bool Validate(PlayerProfile profile, CardCatalog catalog)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            bool changed = false;
            if (profile.SaveVersion != PlayerProfile.CurrentSaveVersion)
            {
                profile.SaveVersion = PlayerProfile.CurrentSaveVersion;
                changed = true;
            }

            if (profile.UnlockedCardIds == null)
            {
                profile.UnlockedCardIds = new List<string>(16);
                changed = true;
            }

            if (profile.ActiveDeckCardIds == null)
            {
                profile.ActiveDeckCardIds = new List<string>(8);
                changed = true;
            }

            if (profile.Shards < 0)
            {
                profile.Shards = 0;
                changed = true;
            }

            changed |= NormalizeCardIds(profile.UnlockedCardIds, catalog, null);

            var ownedCardIds = new HashSet<string>(profile.UnlockedCardIds, StringComparer.Ordinal);
            changed |= NormalizeCardIds(profile.ActiveDeckCardIds, catalog, ownedCardIds);
            if (profile.ActiveDeckCardIds.Count == 0)
            {
                changed |= RestoreDefaultDeck(profile, catalog);
            }

            return changed;
        }

        private static bool RestoreDefaultDeck(PlayerProfile profile, CardCatalog catalog)
        {
            bool changed = false;
            if (profile.UnlockedCardIds.Count == 0)
            {
                catalog.GetStartingCollectionCardIds(profile.UnlockedCardIds);
                changed = profile.UnlockedCardIds.Count > 0;
            }

            var ownedCardIds = new HashSet<string>(profile.UnlockedCardIds, StringComparer.Ordinal);
            var defaultDeckIds = new List<string>(8);
            catalog.GetDefaultDeckCardIds(defaultDeckIds);

            profile.ActiveDeckCardIds.Clear();
            for (int i = 0; i < defaultDeckIds.Count; i++)
            {
                string cardId = defaultDeckIds[i];
                if (ownedCardIds.Contains(cardId) && !profile.ActiveDeckCardIds.Contains(cardId))
                {
                    profile.ActiveDeckCardIds.Add(cardId);
                }
            }

            if (profile.ActiveDeckCardIds.Count == 0)
            {
                for (int i = 0; i < profile.UnlockedCardIds.Count; i++)
                {
                    string cardId = profile.UnlockedCardIds[i];
                    if (catalog.ContainsCardId(cardId) && !profile.ActiveDeckCardIds.Contains(cardId))
                    {
                        profile.ActiveDeckCardIds.Add(cardId);
                    }
                }
            }

            return changed || profile.ActiveDeckCardIds.Count > 0;
        }

        private static bool NormalizeCardIds(List<string> cardIds, CardCatalog catalog, HashSet<string> allowedCardIds)
        {
            bool changed = false;
            var normalized = new List<string>(cardIds.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < cardIds.Count; i++)
            {
                string cardId = cardIds[i];
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    changed = true;
                    continue;
                }

                cardId = cardId.Trim();
                if (!catalog.ContainsCardId(cardId))
                {
                    changed = true;
                    continue;
                }

                if (allowedCardIds != null && !allowedCardIds.Contains(cardId))
                {
                    changed = true;
                    continue;
                }

                if (!seen.Add(cardId))
                {
                    changed = true;
                    continue;
                }

                if (!string.Equals(cardIds[i], cardId, StringComparison.Ordinal))
                {
                    changed = true;
                }

                normalized.Add(cardId);
            }

            if (normalized.Count != cardIds.Count)
            {
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            cardIds.Clear();
            for (int i = 0; i < normalized.Count; i++)
            {
                cardIds.Add(normalized[i]);
            }

            return true;
        }
    }
}

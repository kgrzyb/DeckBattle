using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "CardCatalog", menuName = "Deck Battle/Card Catalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        [SerializeField] private List<CardDefinition> cards = new List<CardDefinition>(16);
        [SerializeField] private List<CardDefinition> startingCollection = new List<CardDefinition>(16);
        [SerializeField] private List<CardDefinition> defaultDeck = new List<CardDefinition>(8);

        private readonly Dictionary<string, CardDefinition> cardById = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        private bool lookupDirty = true;

        public int CardCount
        {
            get { return cards != null ? cards.Count : 0; }
        }

        public IReadOnlyList<CardDefinition> Cards
        {
            get { return cards; }
        }

        public void Configure(
            IReadOnlyList<CardDefinition> allCards,
            IReadOnlyList<CardDefinition> startingCards,
            IReadOnlyList<CardDefinition> defaultDeckCards)
        {
            CopyDefinitions(allCards, cards);
            CopyDefinitions(startingCards, startingCollection);
            CopyDefinitions(defaultDeckCards, defaultDeck);
            lookupDirty = true;
        }

        public bool ContainsCardId(string cardId)
        {
            CardDefinition definition;
            return TryGetCard(cardId, out definition);
        }

        public bool TryGetCard(string cardId, out CardDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            EnsureLookup();
            return cardById.TryGetValue(cardId.Trim(), out definition);
        }

        public void GetAllCardIds(List<string> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            AppendUniqueCardIds(target, cards);
        }

        public void GetStartingCollectionCardIds(List<string> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            int initialCount = target.Count;
            if (HasUsableDefinitions(startingCollection))
            {
                AppendUniqueKnownCardIds(target, startingCollection);
                if (target.Count > initialCount)
                {
                    return;
                }
            }

            AppendUniqueCardIds(target, cards);
        }

        public void GetDefaultDeckCardIds(List<string> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            int initialCount = target.Count;
            if (HasUsableDefinitions(defaultDeck))
            {
                AppendUniqueKnownCardIds(target, defaultDeck);
                if (target.Count > initialCount)
                {
                    return;
                }
            }

            GetStartingCollectionCardIds(target);
        }

        public bool CollectValidationIssues(List<string> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            bool valid = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (!ValidatePrimaryCards(issues, seen))
            {
                valid = false;
            }

            if (!ValidateReferencedCards("Starting Collection", startingCollection, issues))
            {
                valid = false;
            }

            if (!ValidateReferencedCards("Default Deck", defaultDeck, issues))
            {
                valid = false;
            }

            return valid;
        }

        private void OnEnable()
        {
            lookupDirty = true;
        }

        private void OnValidate()
        {
            lookupDirty = true;
#if UNITY_EDITOR
            var issues = new List<string>(8);
            CollectValidationIssues(issues);
            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning(issues[i], this);
            }
#endif
        }

        private void EnsureLookup()
        {
            if (!lookupDirty)
            {
                return;
            }

            cardById.Clear();
            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    CardDefinition card = cards[i];
                    if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                    {
                        continue;
                    }

                    string cardId = card.CardId.Trim();
                    if (!cardById.ContainsKey(cardId))
                    {
                        cardById.Add(cardId, card);
                    }
                }
            }

            lookupDirty = false;
        }

        private bool ValidatePrimaryCards(List<string> issues, HashSet<string> seen)
        {
            bool valid = true;
            if (cards == null || cards.Count == 0)
            {
                issues.Add("CardCatalog has no cards configured.");
                return false;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition card = cards[i];
                if (card == null)
                {
                    issues.Add("CardCatalog contains an empty card reference.");
                    valid = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(card.CardId))
                {
                    issues.Add("CardCatalog contains a card with an empty CardId.");
                    valid = false;
                    continue;
                }

                string cardId = card.CardId.Trim();
                if (!seen.Add(cardId))
                {
                    issues.Add("CardCatalog contains duplicate CardId: " + cardId);
                    valid = false;
                }
            }

            return valid;
        }

        private bool ValidateReferencedCards(string listName, List<CardDefinition> definitions, List<string> issues)
        {
            bool valid = true;
            if (definitions == null)
            {
                return true;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                CardDefinition card = definitions[i];
                if (card == null)
                {
                    issues.Add(listName + " contains an empty card reference.");
                    valid = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(card.CardId))
                {
                    issues.Add(listName + " contains a card with an empty CardId.");
                    valid = false;
                    continue;
                }

                string cardId = card.CardId.Trim();
                if (!ContainsCardId(cardId))
                {
                    issues.Add(listName + " references a card outside the catalog: " + cardId);
                    valid = false;
                }

                if (!seen.Add(cardId))
                {
                    issues.Add(listName + " contains duplicate CardId: " + cardId);
                    valid = false;
                }
            }

            return valid;
        }

        private static void CopyDefinitions(IReadOnlyList<CardDefinition> source, List<CardDefinition> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private static bool HasUsableDefinitions(List<CardDefinition> definitions)
        {
            if (definitions == null)
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                CardDefinition card = definitions[i];
                if (card != null && !string.IsNullOrWhiteSpace(card.CardId))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendUniqueCardIds(List<string> target, List<CardDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                CardDefinition card = definitions[i];
                if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                {
                    continue;
                }

                string cardId = card.CardId.Trim();
                if (!target.Contains(cardId))
                {
                    target.Add(cardId);
                }
            }
        }

        private void AppendUniqueKnownCardIds(List<string> target, List<CardDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                CardDefinition card = definitions[i];
                if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                {
                    continue;
                }

                string cardId = card.CardId.Trim();
                if (ContainsCardId(cardId) && !target.Contains(cardId))
                {
                    target.Add(cardId);
                }
            }
        }
    }
}

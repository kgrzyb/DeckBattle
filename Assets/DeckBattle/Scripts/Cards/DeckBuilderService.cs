using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class DeckBuilderService
    {
        public DeckValidationResult ValidateDeck(PlayerProfile profile, CardCatalog catalog, DeckBuilderRules rules)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            int cardCount = profile.ActiveDeckCardIds != null ? profile.ActiveDeckCardIds.Count : 0;
            int missingCardCount = 0;
            int duplicateCardCount = 0;
            DeckBuildFailReason reason = DeckBuildFailReason.None;

            if (cardCount == 0)
            {
                return new DeckValidationResult(false, 0, 0, 0, DeckBuildFailReason.DeckEmpty);
            }

            var ownedCardIds = new HashSet<string>(StringComparer.Ordinal);
            if (profile.UnlockedCardIds != null)
            {
                for (int i = 0; i < profile.UnlockedCardIds.Count; i++)
                {
                    string ownedCardId = NormalizeCardId(profile.UnlockedCardIds[i]);
                    if (!string.IsNullOrEmpty(ownedCardId) && catalog.ContainsCardId(ownedCardId))
                    {
                        ownedCardIds.Add(ownedCardId);
                    }
                }
            }

            var seenDeckIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cardCount; i++)
            {
                string cardId = NormalizeCardId(profile.ActiveDeckCardIds[i]);
                if (string.IsNullOrEmpty(cardId) || !catalog.ContainsCardId(cardId))
                {
                    missingCardCount++;
                    if (reason == DeckBuildFailReason.None)
                    {
                        reason = DeckBuildFailReason.UnknownCard;
                    }

                    continue;
                }

                if (!ownedCardIds.Contains(cardId))
                {
                    missingCardCount++;
                    if (reason == DeckBuildFailReason.None)
                    {
                        reason = DeckBuildFailReason.CardNotOwned;
                    }
                }

                if (!seenDeckIds.Add(cardId))
                {
                    duplicateCardCount++;
                    if (reason == DeckBuildFailReason.None)
                    {
                        reason = DeckBuildFailReason.AlreadyInDeck;
                    }
                }
            }

            if (reason == DeckBuildFailReason.None && cardCount < rules.NormalizedMinDeckSize)
            {
                reason = DeckBuildFailReason.DeckTooSmall;
            }
            else if (reason == DeckBuildFailReason.None && cardCount > rules.NormalizedMaxDeckSize)
            {
                reason = DeckBuildFailReason.DeckFull;
            }

            return new DeckValidationResult(
                reason == DeckBuildFailReason.None,
                cardCount,
                missingCardCount,
                duplicateCardCount,
                reason);
        }

        public bool CanAddCard(
            PlayerProfile profile,
            string cardId,
            CardCatalog catalog,
            DeckBuilderRules rules,
            out DeckBuildFailReason reason)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            string normalizedCardId = NormalizeCardId(cardId);
            if (string.IsNullOrEmpty(normalizedCardId) || !catalog.ContainsCardId(normalizedCardId))
            {
                reason = DeckBuildFailReason.UnknownCard;
                return false;
            }

            if (!ContainsCardId(profile.UnlockedCardIds, normalizedCardId))
            {
                reason = DeckBuildFailReason.CardNotOwned;
                return false;
            }

            if (ContainsCardId(profile.ActiveDeckCardIds, normalizedCardId))
            {
                reason = DeckBuildFailReason.AlreadyInDeck;
                return false;
            }

            int cardCount = profile.ActiveDeckCardIds != null ? profile.ActiveDeckCardIds.Count : 0;
            if (cardCount >= rules.NormalizedMaxDeckSize)
            {
                reason = DeckBuildFailReason.DeckFull;
                return false;
            }

            reason = DeckBuildFailReason.None;
            return true;
        }

        public bool TryAddCard(
            PlayerProfile profile,
            string cardId,
            CardCatalog catalog,
            DeckBuilderRules rules,
            out DeckBuildFailReason reason)
        {
            if (!CanAddCard(profile, cardId, catalog, rules, out reason))
            {
                return false;
            }

            if (profile.ActiveDeckCardIds == null)
            {
                profile.ActiveDeckCardIds = new List<string>(rules.NormalizedMaxDeckSize);
            }

            profile.ActiveDeckCardIds.Add(NormalizeCardId(cardId));
            return true;
        }

        public bool TryRemoveCard(PlayerProfile profile, string cardId)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.ActiveDeckCardIds == null)
            {
                return false;
            }

            string normalizedCardId = NormalizeCardId(cardId);
            if (string.IsNullOrEmpty(normalizedCardId))
            {
                return false;
            }

            for (int i = 0; i < profile.ActiveDeckCardIds.Count; i++)
            {
                if (string.Equals(NormalizeCardId(profile.ActiveDeckCardIds[i]), normalizedCardId, StringComparison.Ordinal))
                {
                    profile.ActiveDeckCardIds.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCardId(List<string> cardIds, string normalizedCardId)
        {
            if (cardIds == null)
            {
                return false;
            }

            for (int i = 0; i < cardIds.Count; i++)
            {
                if (string.Equals(NormalizeCardId(cardIds[i]), normalizedCardId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeCardId(string cardId)
        {
            return string.IsNullOrWhiteSpace(cardId) ? string.Empty : cardId.Trim();
        }
    }
}

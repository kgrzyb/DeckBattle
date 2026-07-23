using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleStartDataBuilder
    {
        private readonly DeckBuilderService deckBuilderService = new DeckBuilderService();

        public BattleStartData Build(PlayerProfile profile, CardCatalog catalog, BattleStartRules rules, int seed)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var playerDeck = new List<CardDefinition>(rules.DeckRules.NormalizedMaxDeckSize);
            int maxDeckSize = rules.DeckRules.NormalizedMaxDeckSize;
            DeckValidationResult validation = deckBuilderService.ValidateDeck(profile, catalog, rules.DeckRules);
            if (validation.IsValid)
            {
                AppendDefinitions(profile.ActiveDeckCardIds, catalog, playerDeck, maxDeckSize);
            }

            if (playerDeck.Count == 0)
            {
                AppendDefaultDeck(catalog, playerDeck, maxDeckSize);
            }

            var enemyDeck = new List<CardDefinition>(rules.DeckRules.NormalizedMaxDeckSize);
            AppendDefaultDeck(catalog, enemyDeck, maxDeckSize);
            if (enemyDeck.Count == 0 && rules.UsePlayerDeckWhenEnemyDeckMissing)
            {
                AppendDefinitions(playerDeck, enemyDeck, maxDeckSize);
            }

            return new BattleStartData(playerDeck, enemyDeck, seed);
        }

        private static void AppendDefaultDeck(CardCatalog catalog, List<CardDefinition> target, int maxCount)
        {
            var defaultDeckIds = new List<string>(8);
            catalog.GetDefaultDeckCardIds(defaultDeckIds);
            AppendDefinitions(defaultDeckIds, catalog, target, maxCount);
        }

        private static void AppendDefinitions(IReadOnlyList<string> cardIds, CardCatalog catalog, List<CardDefinition> target, int maxCount)
        {
            if (cardIds == null || maxCount <= 0)
            {
                return;
            }

            for (int i = 0; i < cardIds.Count; i++)
            {
                string cardId = cardIds[i];
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    continue;
                }

                CardDefinition definition;
                if (catalog.TryGetCard(cardId, out definition) && definition != null && !target.Contains(definition))
                {
                    target.Add(definition);
                    if (target.Count >= maxCount)
                    {
                        return;
                    }
                }
            }
        }

        private static void AppendDefinitions(IReadOnlyList<CardDefinition> source, List<CardDefinition> target, int maxCount)
        {
            if (source == null || maxCount <= 0)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                CardDefinition definition = source[i];
                if (definition != null && !target.Contains(definition))
                {
                    target.Add(definition);
                    if (target.Count >= maxCount)
                    {
                        return;
                    }
                }
            }
        }
    }
}

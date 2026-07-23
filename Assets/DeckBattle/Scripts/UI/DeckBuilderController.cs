using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class DeckBuilderController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CardCatalog catalog;
        [SerializeField] private int minDeckSize = 1;
        [SerializeField] private int maxDeckSize = 8;

        [Header("Views")]
        [SerializeField] private Transform deckListRoot;
        [SerializeField] private Transform collectionListRoot;
        [SerializeField] private DeckBuilderCardItemView cardItemPrefab;
        [SerializeField] private TMP_Text deckCountText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button saveButton;

        private readonly DeckBuilderService deckBuilderService = new DeckBuilderService();
        private readonly List<string> collectionCardIds = new List<string>(16);

        private PlayerProfileStore profileStore;
        private PlayerProfile profile;
        private bool dirty;

        public bool IsDirty
        {
            get { return dirty; }
        }

        private DeckBuilderRules Rules
        {
            get { return new DeckBuilderRules(minDeckSize, maxDeckSize); }
        }

        private void OnEnable()
        {
            if (saveButton != null)
            {
                saveButton.onClick.AddListener(HandleSaveClicked);
            }
        }

        private void OnDisable()
        {
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(HandleSaveClicked);
            }
        }

        public void Show()
        {
            EnsureProfileLoaded();
            Refresh();
        }

        public bool TrySaveBeforeClose()
        {
            if (!dirty)
            {
                return true;
            }

            return SaveProfile();
        }

        public bool SaveProfile()
        {
            if (!EnsureProfileLoaded())
            {
                return false;
            }

            DeckValidationResult result = deckBuilderService.ValidateDeck(profile, catalog, Rules);
            if (!result.IsValid)
            {
                SetFeedback(FormatValidationMessage(result));
                RefreshSaveState(result);
                return false;
            }

            PlayerProfileStore.Validate(profile, catalog);
            result = deckBuilderService.ValidateDeck(profile, catalog, Rules);
            if (!result.IsValid)
            {
                SetFeedback(FormatValidationMessage(result));
                RefreshSaveState(result);
                return false;
            }

            profileStore.Save(profile);
            dirty = false;
            SetFeedback("Deck saved.");
            RefreshSaveState(result);
            return true;
        }

        private bool EnsureProfileLoaded()
        {
            if (catalog == null)
            {
                Debug.LogError("DeckBuilderController requires a CardCatalog.", this);
                SetFeedback("Card catalog missing.");
                return false;
            }

            if (profileStore == null)
            {
                profileStore = new PlayerProfileStore();
            }

            if (profile == null)
            {
                profile = profileStore.LoadOrCreateDefault(catalog);
                dirty = false;
            }

            return true;
        }

        private void Refresh()
        {
            if (profile == null || catalog == null)
            {
                return;
            }

            DeckValidationResult result = deckBuilderService.ValidateDeck(profile, catalog, Rules);
            if (deckCountText != null)
            {
                deckCountText.text = result.CardCount + " / " + Rules.NormalizedMaxDeckSize;
            }

            RefreshSaveState(result);
            RebuildDeckList();
            RebuildCollectionList();

            if (!result.IsValid)
            {
                SetFeedback(FormatValidationMessage(result));
            }
            else if (!dirty)
            {
                SetFeedback("Deck ready.");
            }
        }

        private void RefreshSaveState(DeckValidationResult result)
        {
            if (saveButton != null)
            {
                saveButton.interactable = dirty && result.IsValid;
            }
        }

        private void RebuildDeckList()
        {
            ClearChildren(deckListRoot);
            if (deckListRoot == null || cardItemPrefab == null || profile.ActiveDeckCardIds == null)
            {
                return;
            }

            for (int i = 0; i < profile.ActiveDeckCardIds.Count; i++)
            {
                CardDefinition card;
                if (!catalog.TryGetCard(profile.ActiveDeckCardIds[i], out card))
                {
                    continue;
                }

                DeckBuilderCardItemView item = Instantiate(cardItemPrefab, deckListRoot);
                item.Bind(card, true, true, RemoveCardFromDeck);
            }
        }

        private void RebuildCollectionList()
        {
            ClearChildren(collectionListRoot);
            if (collectionListRoot == null || cardItemPrefab == null)
            {
                return;
            }

            collectionCardIds.Clear();
            if (profile.UnlockedCardIds != null)
            {
                for (int i = 0; i < profile.UnlockedCardIds.Count; i++)
                {
                    string cardId = profile.UnlockedCardIds[i];
                    if (!string.IsNullOrWhiteSpace(cardId) && !collectionCardIds.Contains(cardId))
                    {
                        collectionCardIds.Add(cardId.Trim());
                    }
                }
            }

            for (int i = 0; i < collectionCardIds.Count; i++)
            {
                CardDefinition card;
                if (!catalog.TryGetCard(collectionCardIds[i], out card))
                {
                    continue;
                }

                DeckBuildFailReason reason;
                bool canAdd = deckBuilderService.CanAddCard(profile, card.CardId, catalog, Rules, out reason);
                bool isInDeck = reason == DeckBuildFailReason.AlreadyInDeck;
                DeckBuilderCardItemView item = Instantiate(cardItemPrefab, collectionListRoot);
                item.Bind(card, isInDeck, canAdd, AddCardToDeck);
            }
        }

        private void AddCardToDeck(string cardId)
        {
            DeckBuildFailReason reason;
            if (deckBuilderService.TryAddCard(profile, cardId, catalog, Rules, out reason))
            {
                dirty = true;
                SetFeedback("Added card.");
                Refresh();
                return;
            }

            SetFeedback(FormatReason(reason));
            Refresh();
        }

        private void HandleSaveClicked()
        {
            SaveProfile();
        }

        private void RemoveCardFromDeck(string cardId)
        {
            if (deckBuilderService.TryRemoveCard(profile, cardId))
            {
                dirty = true;
                SetFeedback("Removed card.");
                Refresh();
            }
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
            }
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private static string FormatValidationMessage(DeckValidationResult result)
        {
            if (result.MissingCardCount > 0)
            {
                return "Deck contains unavailable cards.";
            }

            if (result.DuplicateCardCount > 0)
            {
                return "Deck contains duplicate cards.";
            }

            return FormatReason(result.Reason);
        }

        private static string FormatReason(DeckBuildFailReason reason)
        {
            switch (reason)
            {
                case DeckBuildFailReason.UnknownCard:
                    return "Card is not in the catalog.";
                case DeckBuildFailReason.CardNotOwned:
                    return "Card is not owned.";
                case DeckBuildFailReason.AlreadyInDeck:
                    return "Card is already in the deck.";
                case DeckBuildFailReason.DeckFull:
                    return "Deck is full.";
                case DeckBuildFailReason.DeckTooSmall:
                    return "Deck has too few cards.";
                case DeckBuildFailReason.DeckEmpty:
                    return "Deck is empty.";
                default:
                    return string.Empty;
            }
        }
    }
}

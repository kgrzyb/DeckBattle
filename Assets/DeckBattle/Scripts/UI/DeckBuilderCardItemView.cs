using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class DeckBuilderCardItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CardFaceView faceView;

        private string cardId;
        private Action<string> clicked;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (faceView == null)
            {
                faceView = GetComponentInChildren<CardFaceView>(true);
            }
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void Bind(CardDefinition card, bool isInDeck, bool canAdd, Action<string> onClicked)
        {
            cardId = card != null ? card.CardId : string.Empty;
            clicked = onClicked;

            if (button != null)
            {
                button.interactable = canAdd;
            }

            if (faceView != null)
            {
                faceView.Bind(card);
                faceView.SetVisualState(isInDeck ? CardVisualState.InDeck : canAdd ? CardVisualState.Normal : CardVisualState.Locked);
            }
        }

        private void HandleClick()
        {
            if (button != null && !button.interactable)
            {
                return;
            }

            if (!string.IsNullOrEmpty(cardId) && clicked != null)
            {
                clicked(cardId);
            }
        }
    }
}

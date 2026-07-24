using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class CardFaceView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image cardArtImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.20f, 0.96f);
        [SerializeField] private Color selectedColor = new Color(0.88f, 0.66f, 0.22f, 0.96f);
        [SerializeField] private Color draggingColor = new Color(0.24f, 0.30f, 0.34f, 0.96f);
        [SerializeField] private Color inDeckColor = new Color(0.40f, 0.32f, 0.16f, 0.96f);
        [SerializeField] private Color lockedColor = new Color(0.13f, 0.15f, 0.16f, 0.88f);
        [SerializeField] private Color disabledColor = new Color(0.12f, 0.13f, 0.14f, 0.72f);

        private CardVisualState visualState = CardVisualState.Normal;

        public CardVisualState VisualState
        {
            get { return visualState; }
        }

        private void Awake()
        {
            SetVisualState(visualState);
        }

        public void Bind(CardDefinition definition)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            SetText(nameText, definition.DisplayName);
            SetText(costText, definition.ApCost.ToString());
            SetCardArt(definition.CardArt);
        }

        public void Bind(CardRuntimeState card)
        {
            Bind(card != null ? card.Definition : null);
        }

        public void SetVisualState(CardVisualState state)
        {
            visualState = state;
            if (background != null)
            {
                background.color = GetColor(state);
            }
        }

        public void Clear()
        {
            SetText(nameText, string.Empty);
            SetText(costText, string.Empty);
            SetCardArt(null);
        }

        private Color GetColor(CardVisualState state)
        {
            switch (state)
            {
                case CardVisualState.Selected:
                    return selectedColor;
                case CardVisualState.Dragging:
                    return draggingColor;
                case CardVisualState.InDeck:
                    return inDeckColor;
                case CardVisualState.Locked:
                    return lockedColor;
                case CardVisualState.Disabled:
                    return disabledColor;
                default:
                    return normalColor;
            }
        }

        private void SetCardArt(Sprite sprite)
        {
            if (cardArtImage == null)
            {
                return;
            }

            cardArtImage.sprite = sprite;
            cardArtImage.enabled = sprite != null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}

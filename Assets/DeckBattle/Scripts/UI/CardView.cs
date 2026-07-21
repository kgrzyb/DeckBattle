using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class CardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.20f, 0.96f);
        [SerializeField] private Color selectedColor = new Color(0.88f, 0.66f, 0.22f, 0.96f);
        [SerializeField] private Color draggingColor = new Color(0.24f, 0.30f, 0.34f, 0.96f);
        [SerializeField] private float holdToDragSeconds = 0.18f;
        [SerializeField] private float tapMoveThresholdPixels = 18f;

        private BattleInputController inputController;
        private CardRuntimeState card;
        private bool pointerHeld;
        private bool dragging;
        private bool selected;
        private int pointerId;
        private Vector2 pointerDownScreenPosition;
        private Vector2 lastScreenPosition;
        private float pointerDownTime;

        public CardRuntimeState Card
        {
            get { return card; }
        }

        private void Awake()
        {
            ApplyColor(normalColor);
        }

        private void Update()
        {
            if (!pointerHeld || dragging)
            {
                return;
            }

            if (Time.unscaledTime - pointerDownTime < holdToDragSeconds)
            {
                return;
            }

            BeginDrag(lastScreenPosition);
        }

        public void Bind(CardRuntimeState sourceCard, BattleInputController sourceInputController)
        {
            card = sourceCard;
            inputController = sourceInputController;
            dragging = false;
            pointerHeld = false;
            selected = false;
            ApplyRestingColor();

            CardDefinition definition = card != null ? card.Definition : null;
            if (nameText != null)
            {
                nameText.text = definition != null ? definition.DisplayName : string.Empty;
            }

            if (costText != null)
            {
                costText.text = definition != null ? definition.ApCost.ToString() : string.Empty;
            }

            if (statsText != null)
            {
                UnitDefinition unitDefinition = card != null ? card.UnitDefinition : null;
                SpellDefinition spellDefinition = card != null ? card.SpellDefinition : null;
                statsText.text = unitDefinition != null
                    ? "HP " + unitDefinition.MaxHp + " / ATK " + unitDefinition.Attack + " / RNG " + unitDefinition.AttackRange
                    : spellDefinition != null ? spellDefinition.EffectKind + " " + spellDefinition.Amount
                    : definition != null ? definition.CardKind.ToString() : string.Empty;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerHeld = true;
            dragging = false;
            pointerId = eventData.pointerId;
            pointerDownTime = Time.unscaledTime;
            pointerDownScreenPosition = eventData.position;
            lastScreenPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!pointerHeld || eventData.pointerId != pointerId)
            {
                return;
            }

            lastScreenPosition = eventData.position;
            if (!dragging && Time.unscaledTime - pointerDownTime >= holdToDragSeconds)
            {
                BeginDrag(lastScreenPosition);
            }

            if (dragging && inputController != null)
            {
                inputController.UpdateCardDrag(this, lastScreenPosition);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pointerHeld || eventData.pointerId != pointerId)
            {
                return;
            }

            pointerHeld = false;
            lastScreenPosition = eventData.position;

            if (dragging && inputController != null)
            {
                inputController.EndCardDrag(this, lastScreenPosition);
            }
            else if (IsTap(eventData.position) && inputController != null)
            {
                inputController.HandleCardTap(card);
            }

            dragging = false;
            ApplyRestingColor();
        }

        public void SetSelected(bool isSelected)
        {
            selected = isSelected;
            if (!dragging)
            {
                ApplyRestingColor();
            }
        }

        private void BeginDrag(Vector2 screenPosition)
        {
            if (card == null || inputController == null)
            {
                return;
            }

            dragging = inputController.BeginCardDrag(this, card, screenPosition);
            if (dragging)
            {
                ApplyColor(draggingColor);
            }
        }

        private void ApplyRestingColor()
        {
            ApplyColor(selected ? selectedColor : normalColor);
        }

        private void ApplyColor(Color color)
        {
            if (background != null)
            {
                background.color = color;
            }
        }

        private bool IsTap(Vector2 pointerUpPosition)
        {
            float threshold = tapMoveThresholdPixels * tapMoveThresholdPixels;
            return (pointerUpPosition - pointerDownScreenPosition).sqrMagnitude <= threshold;
        }
    }
}

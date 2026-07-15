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
        [SerializeField] private Color draggingColor = new Color(0.24f, 0.30f, 0.34f, 0.96f);
        [SerializeField] private float holdToDragSeconds = 0.18f;

        private BattleInputController inputController;
        private CardRuntimeState card;
        private bool pointerHeld;
        private bool dragging;
        private int pointerId;
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
            ApplyColor(normalColor);

            UnitDefinition definition = card != null ? card.Definition : null;
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
                statsText.text = definition != null ? "HP " + definition.MaxHp + " / POW " + definition.Power : string.Empty;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerHeld = true;
            dragging = false;
            pointerId = eventData.pointerId;
            pointerDownTime = Time.unscaledTime;
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

            dragging = false;
            ApplyColor(normalColor);
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

        private void ApplyColor(Color color)
        {
            if (background != null)
            {
                background.color = color;
            }
        }
    }
}

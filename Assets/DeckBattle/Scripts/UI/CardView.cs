using UnityEngine;
using UnityEngine.EventSystems;

namespace DeckBattle
{
    public sealed class CardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private CardFaceView faceView;
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
            if (faceView == null)
            {
                faceView = GetComponentInChildren<CardFaceView>(true);
            }

            ApplyRestingVisualState();
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
            if (faceView != null)
            {
                faceView.Bind(card);
            }

            ApplyRestingVisualState();
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
            ApplyRestingVisualState();
        }

        public void SetSelected(bool isSelected)
        {
            selected = isSelected;
            if (!dragging)
            {
                ApplyRestingVisualState();
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
                SetVisualState(CardVisualState.Dragging);
            }
        }

        private void ApplyRestingVisualState()
        {
            SetVisualState(selected ? CardVisualState.Selected : CardVisualState.Normal);
        }

        private void SetVisualState(CardVisualState state)
        {
            if (faceView != null)
            {
                faceView.SetVisualState(state);
            }
        }

        private bool IsTap(Vector2 pointerUpPosition)
        {
            float threshold = tapMoveThresholdPixels * tapMoveThresholdPixels;
            return (pointerUpPosition - pointerDownScreenPosition).sqrMagnitude <= threshold;
        }
    }
}
